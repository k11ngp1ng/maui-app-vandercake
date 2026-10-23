# Disposable real-process restart check. Credentials are generated in memory and never printed.
param([int]$Port = 7187, [switch]$CompleteWorkflow)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$verification = Join-Path $root 'artifacts/verification'
New-Item -ItemType Directory -Force $verification | Out-Null
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('VanderCake-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temporary | Out-Null
$database = Join-Path $temporary 'restart-check.db'
$previous = @{}
foreach ($name in @('ASPNETCORE_ENVIRONMENT','ConnectionStrings__VanderCake','Demo__AdminPassword','Demo__SalespersonPassword')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$process = $null
$checkName = if ($CompleteWorkflow) { 'workflow' } else { 'persistence' }
function Start-TestServer([string]$suffix) {
    $parameters = @{
        FilePath = 'dotnet'
        ArgumentList = @('run', '--project', 'src/VanderCake.Server', '--no-build', '--no-launch-profile',
            '--urls', "https://localhost:$Port")
        WorkingDirectory = $root
        WindowStyle = 'Hidden'
        PassThru = $true
        RedirectStandardOutput = Join-Path $verification ("$checkName-server-$suffix-stdout.txt")
        RedirectStandardError = Join-Path $verification ("$checkName-server-$suffix-stderr.txt")
    }
    $child = Start-Process @parameters
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        if ($child.HasExited) { throw "Server exited: $($child.ExitCode)" }
        try {
            # Local test-client exception only; no application TLS validation is disabled.
            $null = Invoke-RestMethod "https://localhost:$Port/health" -SkipCertificateCheck -TimeoutSec 1
            return $child
        } catch { Start-Sleep -Milliseconds 250 }
    }
    & taskkill /PID $child.Id /T /F | Out-Null
    throw 'Server startup timed out.'
}
function Stop-TestServer($child) {
    if ($null -ne $child -and -not $child.HasExited) {
        & taskkill /PID $child.Id /T /F | Out-Null
        $child.WaitForExit()
    }
}
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__VanderCake = "Data Source=$database;Pooling=False;Foreign Keys=True"
    $env:Demo__AdminPassword = 'aA1!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    $env:Demo__SalespersonPassword = 'aA1!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    Push-Location $root
    try {
        dotnet run --project src/VanderCake.Server --no-build --no-launch-profile -- --migrate --seed-demo *> (Join-Path $verification "$checkName-initialize.txt")
        if ($LASTEXITCODE -ne 0) { throw 'Migration/seed command failed. See the initialization log.' }
    } finally { Pop-Location }
    $process = Start-TestServer 'first'
    $loginBody = @{ email = 'salesperson@vandercake.local'; password = $env:Demo__SalespersonPassword } | ConvertTo-Json
    $token = Invoke-RestMethod "https://localhost:$Port/api/auth/login" -Method Post -Body $loginBody -ContentType 'application/json' -SkipCertificateCheck
    $headers = @{ Authorization = "Bearer $($token.accessToken)" }
    $customers = Invoke-RestMethod "https://localhost:$Port/api/customers" -Headers $headers -SkipCertificateCheck
    $products = Invoke-RestMethod "https://localhost:$Port/api/products" -Headers $headers -SkipCertificateCheck
    $date = [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, [TimeZoneInfo]::FindSystemTimeZoneById('America/Sao_Paulo')).AddDays(1).ToString('yyyy-MM-dd')
    $body = @{ customerId = $customers[0].id; paymentTermCode = 10; dispatchDate = $date; dispatchHour = 19
        submissionKey = [guid]::NewGuid().ToString(); items = @(@{ productId = $products[0].id; quantity = 2 }) } | ConvertTo-Json -Depth 4
    $order = Invoke-RestMethod "https://localhost:$Port/api/orders" -Method Post -Body $body -Headers $headers -ContentType 'application/json' -SkipCertificateCheck
    if ($CompleteWorkflow) {
        $adminBody = @{ email = 'admin@vandercake.local'; password = $env:Demo__AdminPassword } | ConvertTo-Json
        $adminToken = Invoke-RestMethod "https://localhost:$Port/api/auth/login" -Method Post -Body $adminBody -ContentType 'application/json' -SkipCertificateCheck
        $adminHeaders = @{ Authorization = "Bearer $($adminToken.accessToken)" }
        $allocationBody = @{ requestKey = [guid]::NewGuid().ToString(); orders = @(@{
            orderId = $order.id; version = $order.version
            items = @(@{ orderItemId = $order.items[0].id; fulfilledQuantity = 1 })
        }) } | ConvertTo-Json -Depth 6
        $allocation = Invoke-RestMethod "https://localhost:$Port/api/admin/allocations/confirm" -Method Post -Body $allocationBody -Headers $adminHeaders -ContentType 'application/json' -SkipCertificateCheck
        if ($allocation.orders[0].order.status -ne 'PartiallyFulfilled' -or $allocation.orders[0].order.items[0].unfulfilledQuantity -ne 1) {
            throw 'Unexpected allocation outcome.'
        }
        $accountId = $allocation.orders[0].installments[0].id
        $quote = Invoke-RestMethod "https://localhost:$Port/api/receivables/$accountId" -Headers $adminHeaders -SkipCertificateCheck
        $quoteDateText = if ($quote.quoteDate -is [datetime]) { $quote.quoteDate.ToString('yyyy-MM-dd') } else { [string]$quote.quoteDate }
        $paymentBody = @{ requestKey = [guid]::NewGuid().ToString(); version = $quote.version
            quoteDate = $quoteDateText; amountMinor = $quote.payableMinor; confirmZero = ($quote.payableMinor -eq 0) } | ConvertTo-Json
        $payment = Invoke-RestMethod "https://localhost:$Port/api/admin/receivables/$accountId/payments" -Method Post -Body $paymentBody -Headers $adminHeaders -ContentType 'application/json' -SkipCertificateCheck
    }
    Stop-TestServer $process
    $process = Start-TestServer 'second'
    # Reuse the original token to verify persistent Data Protection keys as well as database data.
    $restored = Invoke-RestMethod "https://localhost:$Port/api/orders/$($order.id)" -Headers $headers -SkipCertificateCheck
    if ($restored.id -ne $order.id -or $restored.requestedTotalMinor -ne $order.requestedTotalMinor) {
        throw 'Order did not survive process restart.'
    }
    if ($CompleteWorkflow) {
        $persistedAccount = Invoke-RestMethod "https://localhost:$Port/api/receivables/$accountId" -Headers $adminHeaders -SkipCertificateCheck
        $balance = Invoke-RestMethod "https://localhost:$Port/api/customers/$($order.customerId)/balance" -Headers $headers -SkipCertificateCheck
        $sale = Invoke-RestMethod "https://localhost:$Port/api/sales/$($allocation.orders[0].saleId)" -Headers $headers -SkipCertificateCheck
        if ($restored.status -ne 'PartiallyFulfilled' -or $persistedAccount.status -ne 'Paid' -or $persistedAccount.payment.id -ne $payment.id -or $persistedAccount.payableMinor -ne 0) {
            throw 'Finalized order or payment history did not survive restart.'
        }
        if ($sale.totalMinor -ne $order.items[0].unitPriceMinor -or $balance.openPrincipalMinor -ne $allocation.orders[0].installments[1].principalMinor) {
            throw 'Persisted fulfilled sale or remaining principal is inconsistent.'
        }
        @("UTC: $([DateTimeOffset]::UtcNow.ToString('o'))",
          'HTTPS ORDER -> MANUAL ALLOCATION -> SALE -> RECEIVABLE -> PAYMENT: PASS',
          'Requested 2, fulfilled 1, shortage 1; sale uses only fulfilled quantity.',
          'Normal restart preserved finalized order, sale, installments, paid history and remaining customer debt.',
          'Original salesperson and administrator bearer tokens survived restart.',
          'Result: PASS') | Tee-Object (Join-Path $verification 'workflow-restart.txt')
    }
    else {
    @("UTC: $([DateTimeOffset]::UtcNow.ToString('o'))",
      'Explicit migration and development seeding: PASS',
      'First process: HTTPS bearer login and order creation PASS',
      'Second normal process: original token and persisted order PASS',
      'No startup migration, reseed or database reset was used between processes.',
      'Result: PASS') | Tee-Object (Join-Path $verification 'persistence-restart.txt')
    }
} finally {
    Stop-TestServer $process
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    foreach ($suffix in @('', '-wal', '-shm')) {
        $target = [IO.Path]::GetFullPath($database + $suffix)
        if (-not $target.StartsWith([IO.Path]::GetFullPath($temporary) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unexpected cleanup path.'
        }
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    }
    Remove-Item -LiteralPath $temporary
}
