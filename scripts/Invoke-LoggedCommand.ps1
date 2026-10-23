param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Command,
    [string[]]$CommandArguments = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$logDirectory = Join-Path $root 'artifacts/verification'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logPath = Join-Path $logDirectory ($Name + '.txt')
$started = [DateTimeOffset]::UtcNow.ToString('o')
"Started UTC: $started`nCommand: $Command $($CommandArguments -join ' ')" | Set-Content $logPath
& $Command @CommandArguments 2>&1 | Tee-Object -FilePath $logPath -Append
$code = $LASTEXITCODE
"Exit code: $code`nFinished UTC: $([DateTimeOffset]::UtcNow.ToString('o'))" | Add-Content $logPath
exit $code
