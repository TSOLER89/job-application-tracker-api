param([string]$DatabaseName = 'JobTrackAuthVerification')
$ErrorActionPreference = 'Stop'
if ($DatabaseName -notmatch '^JobTrackAuth[A-Za-z0-9_]+$') {
    throw 'Use a dedicated JobTrackAuth-prefixed verification database.'
}
$verificationConnection = "Server=(localdb)\MSSQLLocalDB;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;"
$env:ConnectionStrings__DefaultConnection = $verificationConnection
dotnet ef database update --project (Join-Path $PSScriptRoot '../job-application-tracker-api.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Could not prepare the verification database.' }
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.SqlClient.SqlConnection $verificationConnection
$connection.Open()
try {
    $command = $connection.CreateCommand()
    $command.CommandText = "IF NOT EXISTS (SELECT 1 FROM JobApplications WHERE Company = 'Legacy ownership verification' AND UserId IS NULL) INSERT INTO JobApplications (Company, Position, Location, Status, Notes) VALUES ('Legacy ownership verification', 'Developer', 'Local', 'Intresserad', '')"
    $command.ExecuteNonQuery() | Out-Null
} finally { $connection.Dispose() }
Write-Output 'Verification database selected in this terminal. Start the API on localhost:5251 here.'
