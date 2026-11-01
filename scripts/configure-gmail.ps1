param(
    [string]$ApiBaseUrl = 'http://localhost:5250',
    [string]$WebBaseUrl = 'http://localhost:5173'
)
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '../job-application-tracker-api.csproj'
$gmailClientId = Read-Host 'Google OAuth Client ID'
if ([string]::IsNullOrWhiteSpace($gmailClientId) -or -not $gmailClientId.EndsWith('.apps.googleusercontent.com')) {
    throw 'Ange Client ID från en Google OAuth-klient av typen Web application.'
}
$gmailClientSecret = Read-Host 'Google OAuth Client Secret (visas inte)' -AsSecureString
if ($gmailClientSecret.Length -eq 0) { throw 'Client Secret saknas.' }
$secretPointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($gmailClientSecret)
try {
    $gmailSettings = @{
        'Email:Gmail:ClientId' = $gmailClientId.Trim()
        'Email:Gmail:ClientSecret' = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
        'Email:Gmail:ApiBaseUrl' = $ApiBaseUrl.TrimEnd('/')
        'Email:Gmail:WebBaseUrl' = $WebBaseUrl.TrimEnd('/')
    }
    # JSON goes through stdin, so secrets are absent from shell history and command-line arguments.
    $gmailSettings | ConvertTo-Json -Compress | dotnet user-secrets set --project $projectPath
    if ($LASTEXITCODE -ne 0) { throw 'Kunde inte spara user-secrets.' }
} finally {
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    $gmailSettings = $null
    $gmailClientSecret.Dispose()
}
Write-Output 'Gmail-konfigurationen är sparad lokalt. Starta om API:t och öppna Mejlkonton i webbappen.'
Write-Output ('Registrerad redirect URI måste vara exakt: ' + $ApiBaseUrl.TrimEnd('/') + '/signin-gmail')
