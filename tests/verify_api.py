"""Integration checks against a running local API; Python standard library only.

Run: python tests/verify_api.py [http://localhost:5251]
Creates and removes its own application and uploaded image, never existing data.
"""

import base64
import json
from pathlib import Path
import re
import sys
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE_URL = (sys.argv.pop(1) if len(sys.argv) > 1 else "http://localhost:5251").rstrip("/")
if urllib.parse.urlsplit(BASE_URL).hostname not in ("localhost", "127.0.0.1", "::1"):
    raise SystemExit("These checks must target a local test server.")
UPLOAD_ROOT = (Path(__file__).resolve().parents[1] / "wwwroot" / "uploads").resolve()
ENDPOINT = "/api/JobApplications"
PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="
)


def request(method, path, body=None, content_type="application/json", headers=None):
    data = json.dumps(body).encode() if isinstance(body, dict) else body
    req = urllib.request.Request(BASE_URL + path, data=data, method=method,
                                 headers={"Content-Type": content_type, **(headers or {})})
    try:
        response = urllib.request.urlopen(req, timeout=45)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.code, response.headers, response.read()


def multipart(data, filename="test.png", mime="image/png", field="file"):
    boundary = "jobtrack-" + uuid.uuid4().hex
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; '
            f'filename="{filename}"\r\nContent-Type: {mime}\r\n\r\n').encode()
    body += data + f"\r\n--{boundary}--\r\n".encode()
    return request("POST", ENDPOINT + "/uploadimage", body, f"multipart/form-data; boundary={boundary}")


class ApiChecks(unittest.TestCase):
    def assert_problem(self, response, status):
        code, headers, body = response
        self.assertEqual(code, status, body.decode(errors="replace"))
        self.assertIn("application/problem+json", headers.get("Content-Type", ""))
        problem = json.loads(body)
        self.assertEqual(problem["status"], status)
        self.assertTrue(problem["title"])
        self.assertIn("traceId", problem)
        return problem

    @staticmethod
    def payload(**changes):
        return dict(company="Verification " + uuid.uuid4().hex, position="Developer",
                    location="Local", status="Intresserad", **changes)

    def test_create_read_update_delete_and_response_compatibility(self):
        application_id = None
        try:
            payload = self.payload(id=2147483647, dateApplied="2026-10-01", notes=None)
            payload["company"] = "  " + payload["company"] + "  "
            code, headers, body = request("POST", ENDPOINT, payload)
            self.assertEqual(code, 201, body)
            saved = json.loads(body)
            application_id = saved["id"]
            self.assertNotEqual(application_id, payload["id"])
            self.assertEqual(saved["company"], payload["company"].strip())
            self.assertEqual(saved["notes"], "")
            self.assertIsNone(saved["dateApplied"])
            self.assertEqual(set(saved), {"id", "company", "position", "location", "status",
                                          "notes", "dateApplied", "imageUrl"})
            location = urllib.parse.urlsplit(headers["Location"]).path
            code, _, body = request("GET", location)
            self.assertEqual(code, 200)
            self.assertEqual(json.loads(body), saved)
            saved.update(status="Intervju", dateApplied="2026-10-01", notes="Updated")
            code, _, body = request("PUT", location, saved)
            self.assertEqual(code, 200, body)
            self.assertEqual(json.loads(body), saved)
            code, _, body = request("GET", ENDPOINT)
            self.assertEqual(code, 200)
            self.assertIn(saved, json.loads(body))
            code, _, _ = request("DELETE", location)
            self.assertEqual(code, 204)
            application_id = None
            self.assert_problem(request("GET", location), 404)
            self.assert_problem(request("DELETE", location), 404)
        finally:
            if application_id is not None:
                self.assertEqual(request("DELETE", f"{ENDPOINT}/{application_id}")[0], 204)

    def test_invalid_fields_return_field_errors(self):
        for field, value in [("company", " "), ("company", None), ("position", ""),
                             ("location", ""), ("status", "Unknown"), ("status", None),
                             ("company", "x" * 201), ("notes", "x" * 5001),
                             ("dateApplied", "2026-02-30"), ("imageUrl", "javascript:alert(1)")]:
            with self.subTest(field=field, value=str(value)[:30]):
                payload = self.payload()
                payload[field] = value
                problem = self.assert_problem(request("POST", ENDPOINT, payload), 400)
                self.assertTrue(problem["errors"])

    def test_missing_and_invalid_routes(self):
        self.assert_problem(request("GET", ENDPOINT + "/2147483647"), 404)
        self.assert_problem(request("PUT", ENDPOINT + "/2147483647", self.payload()), 404)
        self.assert_problem(request("GET", "/missing-test-route"), 404)

    def test_invalid_update_is_validated(self):
        payload = self.payload()
        payload["company"] = ""
        self.assert_problem(request("PUT", ENDPOINT + "/2147483647", payload), 400)

    def test_image_upload_and_static_serving(self):
        code, _, body = multipart(PNG, filename="../../original.PNG", mime="application/octet-stream")
        self.assertEqual(code, 200, body)
        image_url = json.loads(body)["imageUrl"]
        self.assertRegex(image_url, r"^/uploads/[a-f0-9]{32}\.png$")
        file_path = (UPLOAD_ROOT / image_url.rsplit("/", 1)[1]).resolve()
        self.assertEqual(file_path.parent, UPLOAD_ROOT)
        try:
            code, headers, content = request("GET", image_url)
            self.assertEqual(code, 200)
            self.assertEqual(headers["Content-Type"], "image/png")
            self.assertEqual(content, PNG)
        finally:
            # Only this test's server-generated file is eligible for cleanup.
            if re.fullmatch(r"[a-f0-9]{32}\.png", file_path.name):
                for attempt in range(10):
                    try:
                        file_path.unlink(missing_ok=True)
                        break
                    except PermissionError:
                        if attempt == 9:
                            raise
                        # Static-file streaming may release its Windows handle just after EOF.
                        time.sleep(0.1)

    def test_rejects_empty_missing_and_disguised_images(self):
        for data, filename, field in [(b"", "test.png", "file"), (PNG, "test.png", "other"),
                                      (b"<svg></svg>", "test.svg", "file"),
                                      (b"not an image", "test.png", "file"),
                                      (PNG, "test.jpg", "file"), (PNG, "test.html", "file")]:
            with self.subTest(filename=filename, field=field):
                self.assert_problem(multipart(data, filename=filename, field=field), 400)

    def test_rejects_large_image(self):
        self.assert_problem(multipart(PNG + b"0" * (5 * 1024 * 1024)), 413)

    def test_rejects_oversized_request(self):
        self.assert_problem(multipart(PNG + b"0" * (6 * 1024 * 1024)), 413)

    def test_cors_for_web_client(self):
        code, headers, _ = request("OPTIONS", ENDPOINT, headers={
            "Origin": "http://localhost:5173", "Access-Control-Request-Method": "POST",
            "Access-Control-Request-Headers": "content-type"})
        self.assertEqual(code, 204)
        self.assertEqual(headers["Access-Control-Allow-Origin"], "http://localhost:5173")


if __name__ == "__main__":
    unittest.main(verbosity=2)
