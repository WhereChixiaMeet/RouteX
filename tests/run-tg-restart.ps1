$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$dir=Join-Path $PSScriptRoot '.build-tg-restart'
New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item (Join-Path $root 'RouteX.exe') $dir -Force
$sdk=Get-ChildItem (Join-Path $env:ProgramFiles 'dotnet/sdk') -Directory | Sort-Object {[version]($_.Name.Split('-')[0])} -Descending | Select-Object -First 1
$fw=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Web.Extensions') | ForEach-Object {'-r:'+(Join-Path $fw ($_+'.dll'))}
& dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') -nologo -target:exe -nostdlib "-out:$dir/TgRestartTests.exe" "-r:$dir/RouteX.exe" @refs (Join-Path $PSScriptRoot 'TgRestartTests.cs')
if($LASTEXITCODE -ne 0){throw 'TG tests compilation failed'}
& (Join-Path $dir 'TgRestartTests.exe')
if($LASTEXITCODE -ne 0){throw 'TG restart tests failed'}
