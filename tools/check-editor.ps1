param([string]$Unity="D:\workshop\developer_learn\Unity_Hub\2022.3.62f3c1\Editor\Unity.exe")
$ErrorActionPreference="Stop"
$repo=Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Path (Join-Path $repo "logs") -Force | Out-Null
$project=Join-Path $repo "unity-client"
$log=Join-Path $repo "logs\unity-editor-tests.log"
$result=Join-Path $repo "logs\unity-editor-tests.xml"
$arguments='-batchmode -nographics -projectPath "{0}" -runTests -testPlatform EditMode -testResults "{1}" -logFile "{2}"' -f $project,$result,$log
$p=Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0){Get-Content -LiteralPath $log -Tail 55;throw "Editor checks failed: $log"}
[xml]$report=Get-Content -LiteralPath $result -Raw
if($report.'test-run'.result -ne "Passed"){throw "Editor tests did not pass: $result"}
Write-Host "Editor compile/tests passed. No player/server build or archive was created."
