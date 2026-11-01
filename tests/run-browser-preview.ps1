param([switch]$Production,[string]$TestDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$executable=if($Production){'RouteX.exe'}else{'RouteX.WebPreview.exe'}
$dir=Join-Path $PSScriptRoot $(if($Production){'.build-browser-release'}else{'.build-browser'})
if($TestDirectory){$dir=$TestDirectory}
New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item (Join-Path $root $executable) (Join-Path $dir $executable) -Force
$sdk=Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\sdk') -Directory | Sort-Object {[version]($_.Name.Split('-')[0])} -Descending | Select-Object -First 1
$fw=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Web.Extensions') | ForEach-Object {'-r:'+(Join-Path $fw ($_+'.dll'))}
& dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') -nologo -target:exe -nostdlib "-out:$dir\BrowserPreview.exe" "-r:$dir\$executable" @refs (Join-Path $PSScriptRoot 'BrowserPreview.cs')
if($LASTEXITCODE -ne 0){throw 'Browser preview compilation failed'}
$captureDir=Join-Path $root $(if($Production){'tests/.build-browser-release/screenshots'}else{'tests/.build-browser/screenshots'})
& (Join-Path $dir 'BrowserPreview.exe') $captureDir
if($LASTEXITCODE -ne 0){throw 'Browser preview failed'}
