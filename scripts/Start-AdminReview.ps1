# Disposable local UI review: no normal application data or saved credentials are changed.
param([int]$Port = 7194, [switch]$Mobile, [switch]$AcademicFixture)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $root 'artifacts/verification'
New-Item -ItemType Directory -Force $evidence | Out-Null
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('VanderCake-web-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temporary | Out-Null
$previous = @{}
$names = @('ASPNETCORE_ENVIRONMENT','ConnectionStrings__VanderCake','Demo__AdminPassword','Demo__SalespersonPassword')
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $database = Join-Path $temporary 'web-review.db'
    $env:ConnectionStrings__VanderCake = "Data Source=$database;Pooling=False;Foreign Keys=True"
    $env:Demo__AdminPassword = 'aA1!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    $env:Demo__SalespersonPassword = 'aA1!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    Push-Location $root
    try {
        dotnet run --project src/VanderCake.Server --no-build --no-launch-profile -- --migrate --seed-demo "--Demo:AcademicFixture=$($AcademicFixture.IsPresent)" *> (Join-Path $evidence 'web-review-initialize.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Review initialization failed. Inspect web-review-initialize.txt.' }
    } finally { Pop-Location }
    $child = Start-Process dotnet -ArgumentList @('run','--project','src/VanderCake.Server','--no-build','--no-launch-profile','--urls',"https://localhost:$Port") -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidence 'web-review-server-stdout.txt') -RedirectStandardError (Join-Path $evidence 'web-review-server-stderr.txt')
    $ready = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        if ($child.HasExited) { throw 'Review server exited.' }
        try { $null = Invoke-RestMethod "https://localhost:$Port/health" -SkipCertificateCheck -TimeoutSec 1; $ready = $true; break } catch { Start-Sleep -Milliseconds 250 }
    }
    if (!$ready) { throw 'Review server startup timed out.' }
    $body = @{ email = 'salesperson@vandercake.local'; password = $env:Demo__SalespersonPassword } | ConvertTo-Json
    $token = Invoke-RestMethod "https://localhost:$Port/api/auth/login" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck
    $headers = @{ Authorization = "Bearer $($token.accessToken)" }
    $customers = Invoke-RestMethod "https://localhost:$Port/api/customers" -Headers $headers -SkipCertificateCheck
    $products = Invoke-RestMethod "https://localhost:$Port/api/products" -Headers $headers -SkipCertificateCheck
    $product = $products | Where-Object code -eq 'YEAST' | Select-Object -First 1
    $date = [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, [TimeZoneInfo]::FindSystemTimeZoneById('America/Sao_Paulo')).AddDays(1).ToString('yyyy-MM-dd')
    $quantities = @(5,8,9)
    for ($n = 0; !$Mobile -and $n -lt 3; $n++) {
        $order = @{ customerId = $customers[$n].id; paymentTermCode = 10; dispatchDate = $date; dispatchHour = 19; submissionKey = [guid]::NewGuid().ToString(); items = @(@{ productId = $product.id; quantity = $quantities[$n] }) } | ConvertTo-Json -Depth 4
        $null = Invoke-RestMethod "https://localhost:$Port/api/orders" -Method Post -Body $order -Headers $headers -ContentType 'application/json' -SkipCertificateCheck
    }
    # Ignored, temporary credentials for automation; remove this file after review.
    $sessionData = @{ url = "https://localhost:$Port"; processId = $child.Id; database = $database; temporaryDirectory = $temporary; email = 'admin@vandercake.local'; password = $env:Demo__AdminPassword }
    if ($Mobile) { $sessionData.salespersonEmail = 'salesperson@vandercake.local'; $sessionData.salespersonPassword = $env:Demo__SalespersonPassword }
    $sessionData | ConvertTo-Json | Set-Content (Join-Path $evidence 'web-review-session.json')
    Write-Output "Disposable HTTPS review server ready on port $Port. Session metadata is ignored under artifacts/verification."
} catch {
    if ($null -ne $child -and !$child.HasExited) { & taskkill /PID $child.Id /T /F | Out-Null }
    throw
} finally { foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') } }
