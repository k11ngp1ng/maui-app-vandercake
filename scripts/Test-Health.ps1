param([int]$Port = 5187)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$logDirectory = Join-Path $root 'artifacts/verification'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$process = $null
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $startOptions = @{
        FilePath = 'dotnet'
        ArgumentList = @('run', '--project', 'src/VanderCake.Server/VanderCake.Server.csproj',
            '--no-build', '--no-launch-profile', '--urls', "http://127.0.0.1:$Port")
        WorkingDirectory = $root
        WindowStyle = 'Hidden'
        PassThru = $true
        RedirectStandardOutput = Join-Path $logDirectory 'health-server-stdout.txt'
        RedirectStandardError = Join-Path $logDirectory 'health-server-stderr.txt'
    }
    $process = Start-Process @startOptions
    $health = $null
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($process.HasExited) { throw "Server exited with code $($process.ExitCode)" }
        try {
            $health = Invoke-WebRequest "http://127.0.0.1:$Port/health" -TimeoutSec 2
            break
        } catch { Start-Sleep -Milliseconds 250 }
    }
    if ($null -eq $health) { throw 'Health endpoint did not become available.' }
    $body = $health.Content | ConvertFrom-Json
    if ($health.StatusCode -ne 200 -or $body.status -ne 'healthy' -or $body.service -ne 'VanderCake') {
        throw "Unexpected health response: $($health.Content)"
    }
    $page = Invoke-WebRequest "http://127.0.0.1:$Port/" -TimeoutSec 5
    if ($page.StatusCode -ne 200 -or $page.Content -notmatch '<h1>VanderCake</h1>') { throw 'Unexpected Razor response.' }
    @("UTC: $([DateTimeOffset]::UtcNow.ToString('o'))",
      "GET /health -> $($health.StatusCode) $($health.Content)",
      "GET / -> $($page.StatusCode), expected Razor heading present",
      'Result: PASS') | Tee-Object (Join-Path $logDirectory 'health-http.txt')
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        # Terminate only the process tree created by this check.
        & taskkill /PID $process.Id /T /F | Out-Null
    }
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
}

