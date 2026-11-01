# JobTrack – Backend API

This repository contains the backend/API for **JobTrack**, a project developed for the course **Web Applications and Mobile Development**.

The API is built with ASP.NET Core Web API and is used by the React web application. Data is stored in SQL Server LocalDB using Entity Framework Core.

## Features

* Retrieve all job applications
* Retrieve a single job application by ID
* Create a job application
* Update a job application
* Delete a job application
* Upload images
* Serve uploaded images as static files
* CORS configuration for the React frontend
* Swagger for testing the API

## Technologies

* ASP.NET Core Web API
* C#
* .NET 10
* Entity Framework Core
* SQL Server LocalDB
* Swagger / Swashbuckle

## Frontend

Frontend repository:

https://github.com/TSOLER89/job-application-tracker

The frontend is configured to run at:

```text
http://localhost:5173
```

## Prerequisites

To run the backend locally, you need:

* .NET 10 SDK
* SQL Server LocalDB
* Git

The project is configured to use SQL Server LocalDB on Windows.

## Installation and Running the Project

Clone the repository:

```bash
git clone https://github.com/TSOLER89/job-application-tracker-api.git
cd job-application-tracker-api
```

Restore the NuGet packages:

```bash
dotnet restore
```

Create or update the database using the migrations:

```bash
dotnet ef database update
```

If `dotnet ef` is not installed:

```bash
dotnet tool install --global dotnet-ef
```

Start the API:

```bash
dotnet run
```

For a physical phone on the same Wi-Fi, bind the API to all local interfaces:

```bash
dotnet run --urls "http://0.0.0.0:5250"
```

Set the mobile project's `EXPO_PUBLIC_API_BASE_URL` to `http://<your-computer-ip>:5250`.

The API runs at:

```text
http://localhost:5250
```

Swagger is available at:

```text
http://localhost:5250/swagger
```

## Database

The connection string is located in `appsettings.json`.

The database is named:

```text
JobTrackDb
```

Entity Framework Core is used to read and write data.

## API Endpoints

All application and image endpoints require an authenticated account. Requests without a valid session return `401`; another account's application or image returns `404`.

| Method | Endpoint                           | Description                    |
| ------ | ---------------------------------- | ------------------------------ |
| GET    | `/api/JobApplications`             | Retrieves all job applications |
| GET    | `/api/JobApplications/{id}`        | Retrieves one job application  |
| POST   | `/api/JobApplications`             | Creates a job application      |
| PUT    | `/api/JobApplications/{id}`        | Updates a job application      |
| DELETE | `/api/JobApplications/{id}`        | Deletes a job application      |
| POST   | `/api/JobApplications/uploadimage` | Uploads an image               |

## Image Upload

Send a `multipart/form-data` request with the file in the `file` field.
The API accepts JPEG (`.jpg`/`.jpeg`), PNG, and WebP files up to **5 MiB**.
Both the extension and file signature are checked; the client's MIME type is not trusted.
SVG, HTML, unsupported formats, and files whose signature does not match the extension are rejected.
The total upload request is limited to 6 MiB, including multipart metadata.
Server-generated random filenames prevent the client from choosing the storage path.

These checks validate the format's signature; they do not fully decode the image or scan for malware.
Before public hosting, add a dedicated storage service and consider image re-encoding or malware scanning.
See [Microsoft's file upload guidance](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0).

Uploaded images are stored locally in:

```text
App_Data/uploads
```

The API returns a relative image path, for example:

```text
/uploads/abc123.jpg
```

The database stores the image path instead of the actual image file.

The folder containing uploaded files is ignored by Git so that uploaded images are not committed to the repository.

Images are served through the authenticated `/uploads/{fileName}` endpoint, with owner checks and `Cache-Control: private, no-store`. The public static-file middleware is no longer used. Existing files in `wwwroot/uploads` are retained and remain available to the assigned owner through the same protected endpoint.

## Accounts and Sessions

ASP.NET Core Identity manages account creation, password hashes, unique emails, and lockout. Passwords must be 10–128 characters with uppercase, lowercase, and a digit. Five failed password attempts temporarily lock that account for 15 minutes. Registration and login are limited to 30 attempts per minute per client IP.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/api/auth/session` | Current account or `user: null`; also supplies the web CSRF token |
| POST | `/api/auth/register` | Create an account and sign in |
| POST | `/api/auth/login` | Sign in with email and password |
| POST | `/api/auth/logout` | Revoke the current session |

Sessions last up to 30 days and are stored server-side as token hashes. Logout revokes the current device/browser session without signing out other devices.

The web client uses a persistent HttpOnly, SameSite=Strict cookie. Unsafe web requests send the `X-CSRF-TOKEN` value obtained from `/api/auth/session`; browser requests use `credentials: include`. Native clients send `X-Auth-Mode: bearer` when registering or logging in, store the returned opaque `accessToken` in Expo SecureStore, and send `Authorization: Bearer <token>` on API and private-image requests. These session tokens are not JWTs or Gmail/Outlook access tokens.

In Swagger, set `X-Auth-Mode` to `bearer` on login, then paste `accessToken` into **Authorize**. The `.http` file contains disposable-account examples. Do not commit real credentials or session tokens.

### Preserving Existing Applications

The ownership migration adds a nullable `UserId`; it does not delete or automatically assign existing applications. New accounts never claim legacy records just because they register first or use a particular email address.

Before migrating an existing database, take a local application-data snapshot:

```bash
dotnet run -- --backup-legacy true
dotnet ef database update
```

Snapshots are written to the Git-ignored `bin/Backups` folder. After the intended owner creates their own account in the app, a trusted local operator can assign all still-unassigned records:

```bash
dotnet run -- --assign-legacy-to you@example.com
```

This command only updates rows with `UserId = null` and fails if the account does not exist. It does not change another user's records. There is no HTTP endpoint for claiming legacy data. Restart/reload the clients afterward.

### Before Public Deployment

This milestone provides local account access and isolated data. Email verification, password recovery, a disposable demo mode, and Gmail/Microsoft OAuth connections are separate upcoming milestones. Registering a JobTrack account does not verify ownership of the email inbox or connect it for import.

Use HTTPS for production. Serve the web and API on the same site for the strict cookies, configure explicit `Cors:AllowedOrigins`, and persist/protect Data Protection keys for the hosting environment. Local development allows HTTP for Expo Go on a trusted local network. `App_Data` contains private upload storage and Data Protection keys and must never be committed or served publicly. On Windows, persisted keys are protected with DPAPI.

## CORS

CORS is configured so that the React application running at:

```text
http://localhost:5173
```

can communicate with the API.

Native Expo Go requests do not use browser CORS. The phone still needs network access to the API.

## Request Validation

When both dates are supplied for a job whose status is not `Intresserad`, `followUpDate` must be on or after `dateApplied`. Earlier dates return a field validation error on create and update. Follow-ups after `applicationDeadline` are allowed: that deadline concerns sending the application, not contacting the employer afterward. If either date is absent, there is no ordering comparison.

Create and update endpoints accept `JobApplicationRequest` instead of binding the database entity directly.
The response fields are unchanged, preserving compatibility with the web and mobile applications.
The API generates IDs; a client-supplied `id` is ignored.

* `company`, `position`, `location`: required, not whitespace, at most 200 characters each. Values are trimmed before saving.
* `status`: `Intresserad`, `Ansökt`, `Intervju`, `Erbjudande`, or `Avslag`. Defaults to `Ansökt` when omitted.
* `dateApplied`: optional, valid `YYYY-MM-DD` date. Saved as `null` for `Intresserad`.
* `applicationDeadline` and `followUpDate`: optional calendar dates (`YYYY-MM-DD` or `null`). They are independent of status and can be past dates for historical records. Empty strings, timestamps, and invalid calendar dates are rejected. Set `null` to remove a date. PUT replaces all editable fields, so clients must include dates they want to retain.

Apply the additive `AddApplicationPlanningDates` migration with `dotnet ef database update` before starting the updated API. Existing jobs and ownership are preserved; both new fields initially contain `null`. This step stores planning dates; scheduled notifications and upcoming-event summaries are separate stages.
* `notes`: optional, at most 5000 characters; omitted or null values become an empty string.
* `imageUrl`: optional, at most 2048 characters; an upload path or an HTTP/HTTPS URL (for existing records with absolute URLs).

These rules apply to new writes; existing records and the database schema are unchanged.
Creating an application returns `201 Created` with a `Location` header pointing to the detail endpoint.

## Project Structure

```text
Controllers/
└── JobApplicationController.cs

Contracts/
├── JobApplicationRequest.cs
└── JobApplicationResponse.cs

Uploads/
└── ImageUploadPolicy.cs

Data/
└── ApplicationDbContext.cs

Models/
└── JobApplication.cs

Migrations/

wwwroot/
└── uploads/

Program.cs
appsettings.json
```

## Technical Choices

### ASP.NET Core Web API

ASP.NET Core is used to create a REST API with clear endpoints for GET, POST, and PUT. The project also includes DELETE as an additional feature.

### Entity Framework Core

Entity Framework Core is used for database access. It connects the model to SQL Server and allows the database structure to be managed using migrations.

### SQL Server LocalDB

LocalDB was chosen because the project runs locally and works well together with .NET and Entity Framework Core.

### File Upload

Images are stored in the file system, while only the image path is stored in the database. This keeps the database simpler and makes the images available through static files.

## Error Handling and Feedback

The API uses HTTP status codes to provide clear feedback:

* `200 OK` when data is retrieved or updated
* `201 Created` when a new job application is created
* `204 No Content` when a job application is deleted
* `400 Bad Request` for invalid fields, dates, or image formats, or a missing image
* `404 Not Found` when a job application cannot be found
* `413 Content Too Large` when the file or upload request exceeds its limit
* `500 Internal Server Error` for unexpected failures, without exposing exception details

Errors use `application/problem+json` with `title`, `status`, and `traceId`.
Validation errors also include an `errors` object keyed by field name.
Unexpected failures are logged on the server. This follows [ASP.NET Core Problem Details](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).

The API can be tested using Swagger or the `job-application-tracker-api.http` file.

## Integration Checks

With Python 3.10+ installed, use a disposable LocalDB database in a separate terminal instead of the personal application database:

```powershell
.\tests\prepare-local-test-db.ps1
dotnet run --urls "http://localhost:5251"
```

Then run in another terminal from this repository:

```bash
python tests/verify_api.py http://localhost:5251
python tests/verify_auth.py http://localhost:5251
```

The checks exercise CRUD, response compatibility, validation errors, upload limits, file signatures, static images, and web CORS.
They create disposable test accounts, delete their own application records, and remove their own uploaded images. Account checks also verify isolation, private-image ownership, CSRF, session revocation, and lockout. The test database retains test accounts for inspection. Close its server terminal afterward so that its connection-string environment variable is not reused for the normal application database.

## Reflection

I chose a simple structure using a Controller, DbContext, and model because the scope of the project is relatively small. This makes the solution easy to follow without adding extra layers that are not necessary for the assignment.


## Related Repository

Frontend:

https://github.com/TSOLER89/job-application-tracker


Recommended Start Order

When running the complete web application locally:

Start the backend first:

dotnet run

Start the frontend in a second terminal:

npm run dev

Open:

http://localhost:5173

The frontend will then communicate with the backend at:

http://localhost:5250




## Developer

Tsoler Hayitian
.NET Software Developer Student
