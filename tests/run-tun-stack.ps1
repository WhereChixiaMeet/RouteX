$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & cmd /c src\build.cmd
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $testDir = Join-Path $PSScriptRoot '.build-stack'
    New-Item -ItemType Directory -Force $testDir | Out-Null
    Copy-Item RouteX.exe (Join-Path $testDir 'RouteX.exe') -Force
    $sdk = Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\sdk') -Directory | Sort-Object { [version]($_.Name.Split('-')[0]) } -Descending | Select-Object -First 1
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    $references = @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Web.Extensions') | ForEach-Object { '-r:' + (Join-Path $framework ($_ + '.dll')) }
    & dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') -nologo -target:exe -nostdlib "-out:$testDir\TunStackTests.exe" "-r:$testDir\RouteX.exe" @references (Join-Path $PSScriptRoot 'TunStackTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    Push-Location $testDir
    try { & .\TunStackTests.exe; if ($LASTEXITCODE -ne 0) { throw 'Stack checks failed' } }
    finally { Pop-Location }
} finally { Pop-Location }
