$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$dir=Join-Path $PSScriptRoot '.build-ui'
New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item (Join-Path $root 'RouteX.exe') (Join-Path $dir 'RouteX.exe') -Force
$sdk=Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\sdk') -Directory | Sort-Object {[version]($_.Name.Split('-')[0])} -Descending | Select-Object -First 1
$fw=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Web.Extensions') | ForEach-Object {'-r:'+(Join-Path $fw ($_+'.dll'))}
& dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') -nologo -target:exe -nostdlib "-out:$dir\UiReleasePreview.exe" "-r:$dir\RouteX.exe" @refs (Join-Path $PSScriptRoot 'UiReleasePreview.cs')
if($LASTEXITCODE -ne 0){throw 'UI preview compilation failed'}
& (Join-Path $dir 'UiReleasePreview.exe') 'D:\Applications\OpenProjectFolder\outputs\routex-3.6.0-ui'
if($LASTEXITCODE -ne 0){throw 'UI checks failed'}
