$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
 & cmd /c src\build.cmd
 if($LASTEXITCODE -ne 0){throw 'Build failed'}
 $dir=Join-Path $PSScriptRoot '.build-migration'
 New-Item -ItemType Directory -Force $dir | Out-Null
 Copy-Item RouteX.exe (Join-Path $dir 'RouteX.exe') -Force
 $sdk=Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\sdk') -Directory | Sort-Object {[version]($_.Name.Split('-')[0])} -Descending | Select-Object -First 1
 $compiler=Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll'
 $fw=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
 $refs=@('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Web.Extensions','System.Xml') | ForEach-Object {'-r:'+(Join-Path $fw ($_+'.dll'))}
 & dotnet $compiler -nologo -target:exe -nostdlib "-out:$dir\MigrationChild.exe" @refs (Join-Path $PSScriptRoot 'MigrationChild.cs')
 if($LASTEXITCODE -ne 0){throw 'Fake child build failed'}
 & dotnet $compiler -nologo -target:exe -nostdlib "-out:$dir\MigrationTests.exe" "-r:$dir\RouteX.exe" @refs (Join-Path $PSScriptRoot 'MigrationTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Tests build failed'}
 & (Join-Path $dir 'MigrationTests.exe') (Join-Path $dir 'cases') (Join-Path $dir 'MigrationChild.exe')
 if($LASTEXITCODE -ne 0){throw 'Migration checks failed'}
 & dotnet $compiler -nologo -target:exe -nostdlib "-out:$dir\MigrationBootstrap.exe" @refs (Join-Path $PSScriptRoot 'MigrationBootstrap.cs') (Join-Path $repoRoot 'src\NameMigration.cs')
 if($LASTEXITCODE -ne 0){throw 'Bootstrap test build failed'}
 $bootstrap=Join-Path $dir ('bootstrap '+[guid]::NewGuid().ToString('N')+' тест')
 New-Item -ItemType Directory -Force (Join-Path $bootstrap 'config') | Out-Null
 [IO.File]::WriteAllText((Join-Path $bootstrap 'config\settings.ini'),'CONFIG SENTINEL')
 [IO.File]::WriteAllText((Join-Path $bootstrap 'RouteX.exe'),'PREVIOUS ROUTEX')
 Copy-Item (Join-Path $dir 'MigrationBootstrap.exe') (Join-Path $bootstrap 'NetProbe.exe')
 & (Join-Path $bootstrap 'NetProbe.exe') --updated
 if($LASTEXITCODE -ne 0){throw 'Legacy bootstrap failed'}
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while($watch.Elapsed.TotalSeconds -lt 15 -and (!(Test-Path (Join-Path $bootstrap 'bootstrap-ready.txt')) -or (Test-Path (Join-Path $bootstrap 'NetProbe.exe')))){Start-Sleep -Milliseconds 100}
 if(!(Test-Path (Join-Path $bootstrap 'bootstrap-ready.txt')) -or (Test-Path (Join-Path $bootstrap 'NetProbe.exe'))){throw ('Bootstrap did not complete: '+$bootstrap)}
 if((Get-Content (Join-Path $bootstrap 'bootstrap-ready.txt') -Raw) -ne 'RouteX.exe'){throw 'Wrong canonical process name'}
 if((Get-Content (Join-Path $bootstrap 'config\settings.ini') -Raw) -ne 'CONFIG SENTINEL'){throw 'Bootstrap changed config'}
 if((Get-FileHash (Join-Path $bootstrap 'RouteX.exe')).Hash -ne (Get-FileHash (Join-Path $dir 'MigrationBootstrap.exe')).Hash){throw 'Canonical digest mismatch'}
 'PASS full subprocess chain NetProbe.exe -> isolated helper -> RouteX.exe, with readiness acknowledgement and legacy cleanup'
 'PASS full subprocess chain preserved configuration and the staged executable digest'
 $external=Join-Path $dir ('external '+[guid]::NewGuid().ToString('N'))
 $installed=Join-Path $external 'installed app'
 $download=Join-Path $external 'download folder'
 New-Item -ItemType Directory -Force (Join-Path $installed 'config'),$download | Out-Null
 [IO.File]::WriteAllText((Join-Path $installed 'config\settings.ini'),'EXTERNAL INSTALL CONFIG')
 Copy-Item (Join-Path $dir 'MigrationBootstrap.exe') (Join-Path $installed 'NetProbe.exe')
 Copy-Item (Join-Path $dir 'MigrationBootstrap.exe') (Join-Path $download 'RouteX.exe')
 & (Join-Path $download 'RouteX.exe') --test-install-root $installed
 if($LASTEXITCODE -ne 0){throw 'External download bootstrap failed'}
 $watch.Restart()
 while($watch.Elapsed.TotalSeconds -lt 15 -and (!(Test-Path (Join-Path $installed 'bootstrap-ready.txt')) -or (Test-Path (Join-Path $installed 'NetProbe.exe')))){Start-Sleep -Milliseconds 100}
 if(!(Test-Path (Join-Path $installed 'bootstrap-ready.txt')) -or (Test-Path (Join-Path $installed 'NetProbe.exe'))){throw 'External download was not installed into the original folder'}
 if((Get-Content (Join-Path $installed 'config\settings.ini') -Raw) -ne 'EXTERNAL INSTALL CONFIG'){throw 'External installation configuration changed'}
 if(Test-Path (Join-Path $download 'config\settings.ini')){throw 'A second configuration was created in Downloads'}
 'PASS external downloaded RouteX.exe migrated into the existing install without creating another config folder'
 Copy-Item (Join-Path $dir 'MigrationBootstrap.exe') (Join-Path $external 'NetProbe.exe')
 & (Join-Path $external 'NetProbe.exe') --migration-skip
 if($LASTEXITCODE -ne 0 -or !(Test-Path (Join-Path $external 'fallback-ready.txt'))){throw 'Rollback skip flag did not prevent another migration'}
 'PASS rollback launches the legacy fallback without a migration loop'
}finally{Pop-Location}
