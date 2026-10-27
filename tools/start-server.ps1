param([string]$MatchFile="",[string]$Executable="")
$ErrorActionPreference="Stop"
$repo=Split-Path -Parent $PSScriptRoot
if(!$Executable){$Executable=Join-Path $repo "builds\Windows\BoundShoot.exe"}
if(!$MatchFile){
 $latest=Get-ChildItem -LiteralPath (Join-Path $repo "backend\runtime") -Filter "*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
 if(!$latest){throw "No pending match. Prepare four players and click Start in the room first."}
 $MatchFile=$latest.FullName
}
if(!(Test-Path -LiteralPath $Executable)){throw "Build Windows first: .\tools\build.ps1 Windows"}
New-Item -ItemType Directory -Path (Join-Path $repo "logs") -Force | Out-Null
$log=Join-Path $repo "logs\local-game-server.log"
$arguments='-batchmode -nographics -matchFile "{0}" -logFile "{1}"' -f $MatchFile,$log
Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru | Select-Object Id
Write-Host "Server log: $log"
