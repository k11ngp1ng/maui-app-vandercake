# Run interactively; passwords travel to user-secrets through stdin, never command arguments.
param([switch]$AcademicFixture)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
try {
    $admin = Read-Host 'Administrator password (12+ chars, upper/lowercase, digit and symbol)' -AsSecureString
    $seller = Read-Host 'Salesperson password (12+ chars, upper/lowercase, digit and symbol)' -AsSecureString
    function PlainText([Security.SecureString]$value) {
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($value)
        try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    }
    $settings = @{
        'Demo:AdminPassword' = PlainText $admin
        'Demo:SalespersonPassword' = PlainText $seller
    }
    $settings | ConvertTo-Json -Compress | dotnet user-secrets set --project src/VanderCake.Server
    if ($LASTEXITCODE -ne 0) { throw 'Could not store development credentials.' }
    $settings.Clear()
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet run --project src/VanderCake.Server --no-launch-profile -- --migrate --seed-demo "--Demo:AcademicFixture=$($AcademicFixture.IsPresent)"
    if ($LASTEXITCODE -ne 0) { throw 'Development initialization failed; review the error above.' }
    Write-Output 'Ready. Administrator: admin@vandercake.local; salesperson: salesperson@vandercake.local.'
    Write-Output 'Trust a local certificate with dotnet dev-certs https --trust, then use the HTTPS launch profile.'
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    Pop-Location
}
