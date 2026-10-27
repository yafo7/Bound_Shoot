param(
 [ValidateSet("Windows","LinuxServer","Prepare","Test")][string]$Target="Windows",
 [string]$Unity="D:\workshop\developer_learn\Unity_Hub\2022.3.62f3c1\Editor\Unity.exe"
)
$ErrorActionPreference="Stop"
$repo=Split-Path -Parent $PSScriptRoot
$project=Join-Path $repo "unity-client"
New-Item -ItemType Directory -Path (Join-Path $repo "logs") -Force | Out-Null
$log=Join-Path $repo "logs\unity-$Target.log"
$arguments='-batchmode -nographics -projectPath "{0}" -logFile "{1}"' -f $project,$log
if($Target -eq "Test"){
 $arguments+=' -runTests -testPlatform EditMode -testResults "{0}"' -f (Join-Path $repo 'logs\unity-tests.xml')
}else{
 $method=if($Target -eq "Prepare"){"Prepare"}else{$Target}
 $arguments+=" -executeMethod BoundShoot.Editor.BuildTools.$method -quit"
 $env:BOUND_BUILD_DIR=Join-Path $repo "builds\$Target"
}
$p=Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0){Get-Content -LiteralPath $log -Tail 70;throw "Unity exited with code $($p.ExitCode). See $log"}
Write-Host "Completed $Target. Log: $log"
