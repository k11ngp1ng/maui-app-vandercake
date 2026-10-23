# Prepare pending orders, or execute the documented explicit 4/5/6 rehearsal.
# The manifest contains requests/results, never credentials. Keep it for safe retries.
param([string]$BaseUrl = 'https://localhost:7080', [switch]$Complete)
$ErrorActionPreference = 'Stop'
$origin = [uri]$BaseUrl
if ($origin.Scheme -ne 'https' -or $origin.UserInfo -or $origin.AbsolutePath -ne '/' -or $origin.Query -or $origin.Fragment) {
    throw 'BaseUrl must be an HTTPS origin without credentials, path, query or fragment.'
}
$BaseUrl = $origin.GetLeftPart([UriPartial]::Authority)
function Password([string]$environmentName, [string]$prompt) {
    $value = [Environment]::GetEnvironmentVariable($environmentName, 'Process')
    if ($value) { return $value }
    $secure = Read-Host $prompt -AsSecureString
    [Net.NetworkCredential]::new('', $secure).Password
}
function Login([string]$email, [string]$password) {
    $body = @{ email=$email; password=$password } | ConvertTo-Json
    $token = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -Body $body -ContentType 'application/json'
    @{ Authorization="Bearer $($token.accessToken)" }
}
function Get([string]$path, $headers) { $data = Invoke-RestMethod "$BaseUrl$path" -Headers $headers; foreach ($item in $data) { $item } }
function Post([string]$path, $body, $headers) {
    Invoke-RestMethod "$BaseUrl$path" -Method Post -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 20)
}
$seller = Login 'salesperson@vandercake.local' (Password 'Demo__SalespersonPassword' 'Salesperson password')
$admin = Login 'admin@vandercake.local' (Password 'Demo__AdminPassword' 'Administrator password')
$customers = @(Get '/api/customers' $seller | Sort-Object visitOrder)
if ($customers.Count -ne 3) { throw 'Use a dedicated demo database with exactly three assigned customers.' }
$products = @(Get '/api/products' $seller)
$xyz = $products | Where-Object code -eq 'XYZ'
$example = $products | Where-Object code -eq 'EX235'
if (!$xyz -or !$example -or $xyz.priceMinor -ne 1000 -or $example.priceMinor -ne 23500) {
    throw 'Initialize the dedicated database with -AcademicFixture first; expected XYZ=10.00, EX235=235.00.'
}
$folder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VanderCake/demo-manifests'
New-Item -ItemType Directory -Force $folder | Out-Null
# Customer identity binds this manifest to this fixture/database, independently of the port.
$manifestPath = Join-Path $folder ("academic-" + $customers[0].id + '.json')
function Save { $script:manifest | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $manifestPath }
if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
} else {
    $date = [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, [TimeZoneInfo]::FindSystemTimeZoneById('America/Sao_Paulo')).AddDays(1).ToString('yyyy-MM-dd')
    $requests = @()
    $quantities = @(5,8,9,1)
    for ($i=0; $i -lt 4; $i++) {
        $productId = if ($i -lt 3) { $xyz.id } else { $example.id }
        $customerId = if ($i -lt 3) { $customers[$i].id } else { $customers[0].id }
        $requests += @{ customerId=$customerId; paymentTermCode=10; dispatchDate=$date; dispatchHour=19;
            submissionKey=[guid]::NewGuid().ToString(); items=@(@{ productId=$productId; quantity=$quantities[$i] }) }
    }
    $manifest = @{ requests=$requests }
    Save
}
$orders = @($manifest.requests | ForEach-Object { Post '/api/orders' $_ $seller })
if (!$Complete) {
    Write-Output "Prepared/reused four orders: XYZ 5/8/9 plus EX235 1. Manifest: $manifestPath"
    return
}
if (!$manifest.ContainsKey('allocation')) {
    if (@($orders | Where-Object status -ne 'Submitted').Count) {
        throw 'Orders were finalized outside this script. Inspect their existing allocation; do not reset data or retry with new keys.'
    }
    $approved = @(4,5,6,1)
    $selections = @(for ($i=0; $i -lt 4; $i++) {
        @{ orderId=$orders[$i].id; version=$orders[$i].version;
            items=@(@{ orderItemId=$orders[$i].items[0].id; fulfilledQuantity=$approved[$i] }) }
    })
    $manifest.allocation = @{ requestKey=[guid]::NewGuid().ToString(); orders=$selections }
    Save # Persist original versions BEFORE the atomic request, including a lost response.
}
$allocation = Post '/api/admin/allocations/confirm' $manifest.allocation $admin
$xyzOrders = @($allocation.orders | Where-Object { $_.order.items[0].productId -eq $xyz.id })
if (($xyzOrders.order.items.fulfilledQuantity | Measure-Object -Sum).Sum -ne 15 -or
    ($xyzOrders.order.items.unfulfilledQuantity | Measure-Object -Sum).Sum -ne 7) { throw 'Unexpected XYZ allocation.' }
$moves = @($xyzOrders.movements)
if (($moves | Where-Object lotCode -eq 'ACADEMIC-EARLY' | Measure-Object quantity -Sum).Sum -ne 6 -or
    ($moves | Where-Object lotCode -eq 'ACADEMIC-LATER' | Measure-Object quantity -Sum).Sum -ne 9) { throw 'Unexpected FEFO consumption.' }
$exampleOrder = $allocation.orders | Where-Object { $_.order.items[0].productId -eq $example.id }
$exampleSale = Get "/api/sales/$($exampleOrder.saleId)" $admin
if ($exampleSale.totalMinor -ne 23500 -or @($exampleSale.installments).Count -ne 2 -or
    @($exampleSale.installments | Where-Object principalMinor -ne 11750).Count) { throw 'Unexpected 235.00 schedule.' }
$saleDate = [DateOnly]::Parse([string]$exampleSale.saleDate)
$dueDates = @($exampleSale.installments | Sort-Object number)
if ([DateOnly]::Parse([string]$dueDates[0].dueDate) -ne $saleDate.AddDays(7) -or
    [DateOnly]::Parse([string]$dueDates[1].dueDate) -ne $saleDate.AddDays(14)) { throw 'Unexpected due dates.' }
$first = $xyzOrders | Where-Object { $_.order.customerId -eq $customers[0].id }
if (!$manifest.ContainsKey('payment')) {
    $quote = Get "/api/receivables/$($first.installments[0].id)" $admin
    $manifest.accountId = $quote.id
    $manifest.payment = @{ requestKey=[guid]::NewGuid().ToString(); version=$quote.version;
        quoteDate=$quote.quoteDate; amountMinor=$quote.payableMinor; confirmZero=($quote.payableMinor -eq 0) }
    Save
}
$payment = Post "/api/admin/receivables/$($manifest.accountId)/payments" $manifest.payment $admin
$stock = @(Get '/api/admin/stock' $admin) | Where-Object productId -eq $xyz.id
$balances = @($customers | ForEach-Object { Get "/api/customers/$($_.id)/balance" $seller })
if ($stock.physicalQuantity -ne 0 -or ($balances.openPrincipalMinor | Measure-Object -Sum).Sum -ne 36500) {
    throw 'Unexpected final stock or debt (expected total open principal 365.00).'
}
$report = @{ utc=[DateTimeOffset]::UtcNow.ToString('o'); allocationId=$allocation.id;
    fulfilled=15; shortage=7; xyzSaleTotalMinor=15000; xyzStock=$stock; movements=$moves;
    exampleSale=$exampleSale; payment=$payment; customerBalances=$balances; totalOpenPrincipalMinor=36500 }
$reportPath = Join-Path $folder ("report-" + $customers[0].id + '.json')
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $reportPath
Write-Output "PASS: XYZ 5/8/9 -> 4/5/6; FEFO 6+9; stock 0; sales 150.00 + 235.00; eight installments; one settlement; debt 365.00."
Write-Output "Allocation: $($allocation.id). Payment: $($payment.id). Report: $reportPath"

