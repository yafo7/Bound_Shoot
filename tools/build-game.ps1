param(
 [ValidateSet("Windows","MacOS","LinuxServer","QA")][string]$Target="Windows",
 [string]$Unity="D:\UnityEditor\2022.3.62f3\Editor\Unity.exe",
 [string]$ProjectPath=""
)
$ErrorActionPreference="Stop"
$repo=Split-Path -Parent $PSScriptRoot
$project=Join-Path $repo "unity-client"
if($ProjectPath){$project=(Resolve-Path -LiteralPath $ProjectPath).Path}
if(!(Test-Path -LiteralPath $Unity)){throw "Unity Editor missing: $Unity"}
if($Target -eq "MacOS" -and !(Test-Path -LiteralPath (Join-Path (Split-Path $Unity) "Data/PlaybackEngines/MacStandaloneSupport"))){throw "Install Mac Build Support (Mono) for Unity 2022.3.62f3 first."}
if($Target -eq "LinuxServer" -and !(Test-Path -LiteralPath (Join-Path (Split-Path $Unity) "Data/PlaybackEngines/LinuxStandaloneSupport"))){throw "Install Linux Dedicated Server Build Support for Unity 2022.3.62f3 first."}
$output=Join-Path $repo "builds"
New-Item -ItemType Directory -Path (Join-Path $repo "logs") -Force | Out-Null
$method=@{Windows="BuildClient";MacOS="BuildMacClient";LinuxServer="BuildLinuxServer";QA="BuildQaClient"}[$Target]
$platform=@{Windows="Win64";MacOS="OSXUniversal";LinuxServer="Linux64";QA="Win64"}[$Target]
$log=Join-Path $repo "logs\build-$Target.log"
$arguments='-batchmode -nographics -quit -projectPath "{0}" -buildTarget {1} -executeMethod BoundShoot.Editor.BuildTools.{2} -outputRoot "{3}" -logFile "{4}"' -f $project,$platform,$method,$output,$log
$p=Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0){Get-Content -LiteralPath $log -Tail 60;throw "Build failed: $log"}
if(!(Select-String -LiteralPath $log -Pattern "BOUND_BUILD_OK" -Quiet)){throw "Missing build confirmation: $log"}
if($Target -eq "Windows"){
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot "OpenFourClients.cmd") -Destination (Join-Path $output "Windows") -Force
}
if($Target -eq "LinuxServer"){
 $upload=Join-Path $output "UploadToServer"
 $server=Join-Path $upload "server"
 $content=Join-Path $upload "content"
 $deployment=Join-Path $upload "deployment"
 $backend=Join-Path $upload "backend"
 New-Item -ItemType Directory -Path $server,$content,$deployment,$backend -Force | Out-Null
 Get-ChildItem -LiteralPath (Join-Path $output "LinuxServer") | Where-Object {$_.Name -notlike "*_DoNotShip"} | ForEach-Object {
  Copy-Item -LiteralPath $_.FullName -Destination $server -Recurse -Force
 }
 Copy-Item -LiteralPath (Join-Path $project "Assets\BoundShoot\Resources\GameConfig.json") -Destination $content -Force
 Get-ChildItem -LiteralPath (Join-Path $repo "deployment") -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $deployment -Force}
 foreach($name in @("src","migrations","package.json","package-lock.json","tsconfig.json")){
  Copy-Item -LiteralPath (Join-Path (Join-Path $repo "backend") $name) -Destination $backend -Recurse -Force
 }
 Write-Host "Copy the contents of $upload into /home/ubuntu/Bound_Shoot/."
}
Write-Host "Built $Target into $output. No archive was created."
