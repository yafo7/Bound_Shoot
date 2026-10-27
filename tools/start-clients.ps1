param([int]$Count=4,[string]$Backend="http://127.0.0.1:3000")
$ErrorActionPreference="Stop"
$repo=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $repo "builds\Windows\BoundShoot.exe"
if(!(Test-Path -LiteralPath $exe)){throw "Build Windows first."}
for($i=1;$i -le $Count;$i++){
 $arguments='-backend "{0}" -screen-width 960 -screen-height 600 -logFile "{1}"' -f $Backend,(Join-Path $repo "logs\client-$i.log")
 Start-Process -FilePath $exe -ArgumentList $arguments
}
