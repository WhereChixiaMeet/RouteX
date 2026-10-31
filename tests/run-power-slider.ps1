$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & cmd /c src\build.cmd
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    $dir=Join-Path $PSScriptRoot '.build-slider'
    New-Item -ItemType Directory -Force $dir | Out-Null
    Copy-Item RouteX.exe (Join-Path $dir 'RouteX.exe') -Force
    $sdk=Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\sdk') -Directory | Sort-Object {[version]($_.Name.Split('-')[0])} -Descending | Select-Object -First 1
    $framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    $refs=@('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Web.Extensions','Accessibility') | ForEach-Object {'-r:'+(Join-Path $framework ($_+'.dll'))}
    & dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') -nologo -target:exe -nostdlib "-out:$dir\PowerSliderPreview.exe" "-r:$dir\RouteX.exe" @refs (Join-Path $PSScriptRoot 'PowerSliderPreview.cs')
    if($LASTEXITCODE -ne 0){throw 'Preview compilation failed'}
    & (Join-Path $dir 'PowerSliderPreview.exe') 'D:\Applications\OpenProjectFolder\outputs\routex-slider-preview'
    if($LASTEXITCODE -ne 0){throw 'Preview checks failed'}
} finally {Pop-Location}
