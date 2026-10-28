param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../output/南京行程表_柔和侧栏版_20261028'), [string]$PackageDirectory = (Join-Path $PSScriptRoot '.packages'))
$ErrorActionPreference = 'Stop'
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
Push-Location -LiteralPath $PSScriptRoot
try {
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:MSBuildEnableWorkloadResolver = 'false'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:TEMP = Join-Path $PSScriptRoot '.temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "构建或测试失败，退出代码 $LASTEXITCODE" } }
dotnet restore Planner.Tests/Planner.Tests.csproj --packages $PackageDirectory --ignore-failed-sources --disable-parallel --disable-build-servers -m:1 -p:UseSharedCompilation=false
Check-Exit
dotnet build Planner.Tests/Planner.Tests.csproj -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
Check-Exit
dotnet Planner.Tests/bin/Release/net10.0/Planner.Tests.dll
Check-Exit
dotnet restore Planner.Desktop/Planner.Desktop.csproj -r win-x64 --packages $PackageDirectory --ignore-failed-sources -p:SelfContained=true --disable-parallel --disable-build-servers -m:1 -p:UseSharedCompilation=false
Check-Exit
dotnet publish Planner.Desktop/Planner.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -o $OutputDirectory
Check-Exit
Copy-Item -LiteralPath README.md -Destination (Join-Path $OutputDirectory '使用说明.md') -Force
Write-Output "已生成：$OutputDirectory"
} finally { Pop-Location }
