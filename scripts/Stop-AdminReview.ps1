# Stop only the disposable UI review created by Start-AdminReview.ps1.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$metadataPath = Join-Path $root 'artifacts/verification/web-review-session.json'
if (!(Test-Path -LiteralPath $metadataPath)) { Write-Output 'No review session metadata.'; exit 0 }
$review = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$directory = [IO.Path]::GetFullPath($review.temporaryDirectory)
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
if (!$directory.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $directory -Leaf) -notmatch '^VanderCake-web-[a-f0-9]{32}$') { throw 'Unexpected temporary directory; refusing cleanup.' }
$database = [IO.Path]::GetFullPath($review.database)
if ($database -ne (Join-Path $directory 'web-review.db')) { throw 'Unexpected review database; refusing cleanup.' }
$running = Get-CimInstance Win32_Process -Filter "ProcessId = $($review.processId)"
if ($null -ne $running) {
    if ($running.Name -ne 'dotnet.exe' -or $running.CommandLine -notlike '*--project src/VanderCake.Server*' -or $running.CommandLine -notlike "*--urls $($review.url)*") { throw 'Unexpected process; refusing to stop it.' }
    & taskkill /PID $review.processId /T /F | Out-Null
}
foreach ($suffix in @('', '-wal', '-shm')) { $target = $database + $suffix; if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target } }
# Non-recursive removal deliberately fails if anything unexpected is present.
if ((Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory }
Remove-Item -LiteralPath $metadataPath
Write-Output 'Disposable review server, database and generated credential metadata removed. Screenshots and logs preserved.'
