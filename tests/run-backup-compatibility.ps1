$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$testDir=Join-Path $PSScriptRoot '.build-backup'
New-Item -ItemType Directory -Force $testDir | Out-Null
$sdk=Get-ChildItem (Join-Path $env:ProgramFiles 'dotnet/sdk') -Directory | Sort-Object {[version]($_.Name.Split('-')[0])} -Descending | Select-Object -First 1
$fw=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('mscorlib','System','System.Core','System.Web.Extensions') | ForEach-Object {'-r:'+(Join-Path $fw ($_+'.dll'))}
& dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') -nologo -target:exe -nostdlib "-out:$testDir/BackupCompatibility.exe" @refs (Join-Path $repoRoot 'src/Backup.cs') (Join-Path $PSScriptRoot 'BackupCompatibility.cs')
if($LASTEXITCODE -ne 0){throw 'Backup test compilation failed'}
& (Join-Path $testDir 'BackupCompatibility.exe')
if($LASTEXITCODE -ne 0){throw 'Backup compatibility checks failed'}
