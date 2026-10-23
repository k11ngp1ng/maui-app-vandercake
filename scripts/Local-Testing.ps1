param([switch]$Stop, [switch]$NoBrowser, [switch]$NoApp)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $root 'artifacts/local-run'
$sessionPath = Join-Path $folder 'session.json'
if (!(Test-Path -LiteralPath $sessionPath)) {
    throw 'Sessao de testes ausente. Use Configurar-Projeto.cmd e Iniciar-Servidor.cmd para configurar seu ambiente; veja docs/COMO_RODAR.md.'
}
$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
$uri = [uri]$session.url
if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'localhost' -or $uri.AbsolutePath -ne '/' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) {
    throw 'A sessao deve apontar para uma origem HTTPS localhost.'
}
$url = $uri.GetLeftPart([UriPartial]::Authority)
if ($Stop) {
    $owned = Get-CimInstance Win32_Process -Filter ("ProcessId = " + [int]$session.serverProcessId) -ErrorAction SilentlyContinue
    if (!$owned) { Write-Output 'Servidor de testes ja esta parado.'; return }
    if ($owned.Name -ne 'dotnet.exe' -or $owned.CommandLine -notmatch [regex]::Escape('src/VanderCake.Server') -or
        $owned.CommandLine -notmatch [regex]::Escape($url)) {
        throw 'O PID salvo nao corresponde ao servidor de testes. Nenhum processo foi encerrado.'
    }
    & taskkill /PID $owned.ProcessId /T /F
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel encerrar o servidor de testes.' }
    Write-Output 'Servidor de testes parado. Banco preservado. Feche o aplicativo separadamente.'
    return
}
if (!(Test-Path -LiteralPath $session.database)) { throw 'Banco de testes nao encontrado. Nenhum banco sera apagado ou recriado.' }
$healthy = $false
try {
    $health = Invoke-RestMethod "$url/health" -TimeoutSec 2
    $healthy = $health.service -eq 'VanderCake' -and $health.status -eq 'healthy'
} catch {}
if (!$healthy) {
    $previous = @{}
    foreach ($name in @('ASPNETCORE_ENVIRONMENT','ConnectionStrings__VanderCake')) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:ConnectionStrings__VanderCake = "Data Source=$($session.database);Foreign Keys=True"
        $server = Start-Process dotnet -ArgumentList @('run','--project','src/VanderCake.Server','--no-build','--no-launch-profile','--urls',$url) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'testing-server-stdout.txt') -RedirectStandardError (Join-Path $folder 'testing-server-stderr.txt')
    } finally {
        foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    }
    $session | Add-Member -NotePropertyName serverProcessId -NotePropertyValue $server.Id -Force
    $session | ConvertTo-Json | Set-Content -LiteralPath $sessionPath
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        if ($server.HasExited) { throw 'Servidor encerrou. Consulte artifacts/local-run/testing-server-stderr.txt; verifique se a porta esta ocupada.' }
        try {
            $health = Invoke-RestMethod "$url/health" -TimeoutSec 1
            if ($health.service -eq 'VanderCake' -and $health.status -eq 'healthy') { $healthy=$true; break }
        } catch {}
        Start-Sleep -Milliseconds 300
    }
}
if (!$healthy) { throw 'Servidor de testes nao respondeu. Consulte os logs em artifacts/local-run.' }
foreach ($account in @(@{email=$session.adminEmail;password=$session.adminPassword},@{email=$session.salespersonEmail;password=$session.salespersonPassword})) {
    $null = Invoke-RestMethod "$url/api/auth/login" -Method Post -ContentType 'application/json' -Body ($account | ConvertTo-Json)
}
if (!$NoApp) {
    $appPath = Join-Path $root 'src/VanderCake.Mobile/bin/Debug/net10.0-windows10.0.19041.0/win-x64/VanderCake.Mobile.exe'
    if (!(Test-Path -LiteralPath $appPath)) { throw 'Aplicativo nao compilado. Execute Configurar-Projeto.cmd antes.' }
    $running = Get-Process VanderCake.Mobile -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $appPath }
    if (!$running) { $null = Start-Process $appPath }
}
if (!$NoBrowser) { Start-Process "$url/Admin" }
Write-Output ''
Write-Output 'SERVIDOR DE TESTES PRONTO. Ambos os logins foram verificados.'
Write-Output "Site: $url/Admin"
Write-Output "API no aplicativo Windows: $url"
Write-Output "Administrador: $($session.adminEmail)"
Write-Output "Senha do administrador: $($session.adminPassword)"
Write-Output "Vendedor: $($session.salespersonEmail)"
Write-Output "Senha do vendedor: $($session.salespersonPassword)"
Write-Output ''
Write-Output 'Credenciais privadas tambem estao em artifacts/local-run/ACCESS.md.'
Write-Output 'O banco existente foi preservado. Este comando nao migra, redefine senhas nem repoe estoque.'
Write-Output 'Para encerrar apenas este servidor, use Parar-Teste.cmd. Fechar esta janela nao o encerra.'
