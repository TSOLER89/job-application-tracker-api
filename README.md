# JobTrack – Backend API

This repository contains the backend/API for **JobTrack**, a project developed for the course **Web Applications and Mobile Development**.

The API is built with ASP.NET Core Web API and is used by the React web application. Data is stored in SQL Server LocalDB using Entity Framework Core.

## Features

* Retrieve all job applications
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
| POST   | `/api/JobApplications`             | Creates a job application      |
| PUT    | `/api/JobApplications/{id}`        | Updates a job application      |
| DELETE | `/api/JobApplications/{id}`        | Deletes a job application      |
| POST   | `/api/JobApplications/uploadimage` | Uploads an image               |

## Image Upload

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

## Project Structure

```text
Controllers/
└── JobApplicationController.cs

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
* `400 Bad Request` when an uploaded file is missing
* `404 Not Found` when a job application cannot be found

The API can be tested using Swagger or the `job-application-tracker-api.http` file.

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