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
wwwroot/uploads
```

The API returns a relative image path, for example:

```text
/uploads/abc123.jpg
```

The database stores the image path instead of the actual image file.

The folder containing uploaded files is ignored by Git so that uploaded images are not committed to the repository.

## CORS

CORS is configured so that the React application running at:

```text
http://localhost:5173
```

can communicate with the API.

Native Expo Go requests do not use browser CORS. The phone still needs network access to the API.

## Request Validation

Create and update endpoints accept `JobApplicationRequest` instead of binding the database entity directly.
The response fields are unchanged, preserving compatibility with the web and mobile applications.
The API generates IDs; a client-supplied `id` is ignored.

* `company`, `position`, `location`: required, not whitespace, at most 200 characters each. Values are trimmed before saving.
* `status`: `Intresserad`, `Ansökt`, `Intervju`, `Erbjudande`, or `Avslag`. Defaults to `Ansökt` when omitted.
* `dateApplied`: optional, valid `YYYY-MM-DD` date. Saved as `null` for `Intresserad`.
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

With Python 3.10+ installed, start the updated API in one terminal:

```bash
dotnet run --urls "http://localhost:5251"
```

Then run in another terminal from this repository:

```bash
python tests/verify_api.py http://localhost:5251
```

The checks exercise CRUD, response compatibility, validation errors, upload limits, file signatures, static images, and web CORS.
They use the configured local database, create and delete their own test application, and remove their own uploaded image from this repository's upload directory. Run against a local test server started from this repository; existing records are not edited.

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
