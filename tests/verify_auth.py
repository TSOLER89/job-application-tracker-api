"""Account isolation checks. Run against a disposable local database/server only.

python tests/verify_auth.py http://localhost:5251
Creates test accounts; does not create or use real passwords or email connections.
"""
import base64
import http.cookiejar
import json
from pathlib import Path
import sys
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = (sys.argv.pop(1) if len(sys.argv) > 1 else 'http://localhost:5251').rstrip('/')
if urllib.parse.urlsplit(BASE).hostname not in ('localhost', '127.0.0.1', '::1'):
    raise SystemExit('Use a local disposable test server.')
PNG = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')
PASSWORD = 'Verification12345'
API = '/api/JobApplications'


def call(method, path, data=None, token=None, headers=None, opener=None):
    headers = {'Content-Type': 'application/json', **(headers or {})}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if isinstance(data, dict):
        data = json.dumps(data).encode()
    req = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    try:
        response = (opener.open if opener else urllib.request.urlopen)(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.code, response.headers, response.read()


def account():
    email = 'verify-' + uuid.uuid4().hex + '@example.test'
    result = call('POST', '/api/auth/register', {'email': email, 'password': PASSWORD}, headers={'X-Auth-Mode': 'bearer'})
    if result[0] != 201:
        raise AssertionError(result)
    return email, json.loads(result[2])


def application(**changes):
    return {'company': 'Verification ' + uuid.uuid4().hex, 'position': 'Developer',
            'location': 'Local', 'status': 'Intresserad', **changes}


class AuthChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.email_a, cls.a = account()
        cls.email_b, cls.b = account()
        cls.files = []

    @classmethod
    def tearDownClass(cls):
        root = (Path(__file__).resolve().parents[1] / 'App_Data' / 'uploads').resolve()
        for file_name in cls.files:
            target = (root / file_name).resolve()
            if target.parent != root:
                raise AssertionError('Unsafe test cleanup path')
            for attempt in range(10):
                try:
                    target.unlink(missing_ok=True)
                    break
                except PermissionError:
                    if attempt == 9:
                        raise
                    time.sleep(0.1)

    def test_anonymous_cannot_access_applications_or_uploads(self):
        for method, path, data in [('GET', API, None), ('GET', API + '/1', None),
                                   ('POST', API, application()), ('PUT', API + '/1', application()),
                                   ('DELETE', API + '/1', None), ('GET', '/uploads/unknown.png', None)]:
            with self.subTest(method=method, path=path):
                code, _, body = call(method, path, data)
                self.assertEqual(code, 401, body)

    def test_registration_validation_duplicate_and_login(self):
        code, _, _ = call('POST', '/api/auth/register', {'email': 'weak@example.test', 'password': 'weak'}, headers={'X-Auth-Mode': 'bearer'})
        self.assertEqual(code, 400)
        code, _, _ = call('POST', '/api/auth/register', {'email': self.email_a, 'password': PASSWORD}, headers={'X-Auth-Mode': 'bearer'})
        self.assertEqual(code, 400)
        code, _, _ = call('POST', '/api/auth/login', {'email': self.email_a, 'password': 'Incorrect12345'}, headers={'X-Auth-Mode': 'bearer'})
        self.assertEqual(code, 401)
        code, _, body = call('POST', '/api/auth/login', {'email': self.email_a.upper(), 'password': PASSWORD}, headers={'X-Auth-Mode': 'bearer'})
        self.assertEqual(code, 200, body)
        self.assertEqual(json.loads(body)['user']['id'], self.a['user']['id'])

    def test_accounts_cannot_read_update_delete_or_assign_other_accounts_data(self):
        token_a, token_b = self.a['accessToken'], self.b['accessToken']
        payload = application(userId=self.b['user']['id'])
        code, _, body = call('POST', API, payload, token_a)
        self.assertEqual(code, 201, body)
        saved = json.loads(body)
        location = API + '/' + str(saved['id'])
        try:
            self.assertNotIn('userId', saved)
            self.assertIn(saved, json.loads(call('GET', API, token=token_a)[2]))
            self.assertNotIn(saved, json.loads(call('GET', API, token=token_b)[2]))
            for method, data in [('GET', None), ('PUT', application()), ('DELETE', None)]:
                self.assertEqual(call(method, location, data, token_b)[0], 404)
            self.assertEqual(call('GET', location, token=token_a)[0], 200)
        finally:
            self.assertEqual(call('DELETE', location, token=token_a)[0], 204)

    def test_new_accounts_do_not_claim_preserved_legacy_records(self):
        for session in (self.a, self.b):
            records = json.loads(call('GET', API, token=session['accessToken'])[2])
            self.assertFalse(any(record['company'] == 'Legacy ownership verification' for record in records))

    def test_private_images_and_image_reference_ownership(self):
        boundary = 'jobtrack-' + uuid.uuid4().hex
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="test.png"\r\n'
                'Content-Type: image/png\r\n\r\n').encode() + PNG + f'\r\n--{boundary}--\r\n'.encode()
        code, _, response = call('POST', API + '/uploadimage', body, self.a['accessToken'],
                                 {'Content-Type': 'multipart/form-data; boundary=' + boundary})
        self.assertEqual(code, 200, response)
        url = json.loads(response)['imageUrl']
        self.files.append(url.rsplit('/', 1)[1])
        self.assertEqual(call('GET', url)[0], 401)
        self.assertEqual(call('GET', url, token=self.b['accessToken'])[0], 404)
        code, headers, content = call('GET', url, token=self.a['accessToken'])
        self.assertEqual(code, 200)
        self.assertEqual(content, PNG)
        self.assertIn('no-store', headers['Cache-Control'])
        for foreign_url in (url, url.replace('/uploads/', '/UPLOADS/')):
            code, _, _ = call('POST', API, application(imageUrl=foreign_url), self.b['accessToken'])
            self.assertEqual(code, 400)

    def test_cookie_session_requires_csrf_and_does_not_expose_bearer_token(self):
        jar = http.cookiejar.CookieJar()
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
        csrf = json.loads(call('GET', '/api/auth/session', opener=opener)[2])['csrfToken']
        payload = {'email': 'cookie-' + uuid.uuid4().hex + '@example.test', 'password': PASSWORD}
        self.assertEqual(call('POST', '/api/auth/register', payload, opener=opener)[0], 400)
        code, headers, body = call('POST', '/api/auth/register', payload, headers={'X-CSRF-TOKEN': csrf}, opener=opener)
        self.assertEqual(code, 201, body)
        session = json.loads(body)
        self.assertIsNone(session['accessToken'])
        cookies = headers.get_all('Set-Cookie')
        self.assertTrue(any('JobTrack.Session=' in cookie and 'httponly' in cookie.lower() and 'samesite=strict' in cookie.lower() for cookie in cookies))
        self.assertEqual(call('POST', API, application(), opener=opener)[0], 400)
        self.assertEqual(call('POST', API, application(), headers={'X-Auth-Mode': 'bearer'}, opener=opener)[0], 400)
        csrf_headers = {'X-CSRF-TOKEN': session['csrfToken']}
        code, _, body = call('POST', API, application(), headers=csrf_headers, opener=opener)
        self.assertEqual(code, 201, body)
        saved = json.loads(body)
        self.assertEqual(call('DELETE', API + '/' + str(saved['id']), headers=csrf_headers, opener=opener)[0], 204)
        self.assertEqual(call('POST', '/api/auth/logout', headers=csrf_headers, opener=opener)[0], 204)
        self.assertEqual(call('GET', API, opener=opener)[0], 401)

    def test_untrusted_web_origin_has_no_credentialed_cors_access(self):
        _, headers, _ = call('OPTIONS', '/api/auth/login', headers={'Origin': 'https://untrusted.example', 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'x-auth-mode'})
        self.assertIsNone(headers.get('Access-Control-Allow-Origin'))

    def test_logout_revokes_only_the_selected_session(self):
        payload = {'email': self.email_a, 'password': PASSWORD}
        code, _, body = call('POST', '/api/auth/login', payload, headers={'X-Auth-Mode': 'bearer'})
        self.assertEqual(code, 200)
        token = json.loads(body)['accessToken']
        self.assertEqual(call('POST', '/api/auth/logout', token=token)[0], 204)
        self.assertEqual(call('GET', API, token=token)[0], 401)
        self.assertEqual(call('GET', API, token=self.a['accessToken'])[0], 200)

    def test_z_password_failures_lock_only_the_target_account(self):
        for _ in range(5):
            self.assertEqual(call('POST', '/api/auth/login', {
                'email': self.email_b, 'password': 'Incorrect12345'
            }, headers={'X-Auth-Mode': 'bearer'})[0], 401)
        self.assertEqual(call('POST', '/api/auth/login', {
            'email': self.email_b, 'password': PASSWORD
        }, headers={'X-Auth-Mode': 'bearer'})[0], 401)
        self.assertEqual(call('POST', '/api/auth/login', {
            'email': self.email_a, 'password': PASSWORD
        }, headers={'X-Auth-Mode': 'bearer'})[0], 200)


if __name__ == '__main__':
    unittest.main(verbosity=2)
