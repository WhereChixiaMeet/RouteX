@echo off
setlocal
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set DefineConstants=WEB_UI
set PreviewManifest=app.manifest
set PreviewOutput=..\RouteX.WebPreview.exe
if "%~1"=="release" set PreviewOutput=..\RouteX.exe
if "%~1"=="review" (
 set DefineConstants=WEB_UI,UI_PREVIEW
 set PreviewManifest=ui-preview.manifest
)
for /f "delims=" %%d in ('dir /b /ad /o-n "C:\Program Files\dotnet\sdk"') do if not defined SDK set "SDK=C:\Program Files\dotnet\sdk\%%d"
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File prepare-web-ui.ps1
if errorlevel 1 exit /b 1
set WV=..\vendor\webview2\sdk
rem Long-lived monitor probe (started as bin\agent\netprobe-vpn.exe / netprobe-direct.exe), embedded below
dotnet "%SDK%\Roslyn\bincore\csc.dll" -nologo -deterministic+ -target:winexe -optimize+ -nostdlib -platform:anycpu -out:ProbeAgent\probe-agent.exe -r:"%FW%\mscorlib.dll" -r:"%FW%\System.dll" ProbeAgent\ProbeAgent.cs
if errorlevel 1 exit /b 1
dotnet "%SDK%\Roslyn\bincore\csc.dll" -nologo -deterministic+ -define:%DefineConstants% -target:winexe -optimize+ -langversion:latest -nostdlib -platform:anycpu -win32icon:app.ico -resource:app-icon.png,NetProbe.AppIcon.png -resource:ProbeAgent\probe-agent.exe,RouteX.ProbeAgent.exe -resource:WebUi\interface.html,RouteX.Browser.Interface.html -resource:..\docs\licenses\WebView2-LICENSE.txt,RouteX.Licenses.WebView2.txt -resource:..\docs\licenses\WebView2-NOTICE.txt,RouteX.Licenses.WebView2Notice.txt -resource:..\docs\licenses\Lucide-LICENSE.txt,RouteX.Licenses.Lucide.txt -resource:"%WV%\lib\net462\Microsoft.Web.WebView2.Core.dll",RouteX.Browser.Microsoft.Web.WebView2.Core.dll -resource:"%WV%\lib\net462\Microsoft.Web.WebView2.WinForms.dll",RouteX.Browser.Microsoft.Web.WebView2.WinForms.dll -resource:"%WV%\runtimes\win-x64\native\WebView2Loader.dll",RouteX.Browser.Loader.x64 -resource:"%WV%\runtimes\win-x86\native\WebView2Loader.dll",RouteX.Browser.Loader.x86 -win32manifest:%PreviewManifest% -out:%PreviewOutput% -r:"%WV%\lib\net462\Microsoft.Web.WebView2.Core.dll" -r:"%WV%\lib\net462\Microsoft.Web.WebView2.WinForms.dll" -r:"%FW%\mscorlib.dll" -r:"%FW%\System.dll" -r:"%FW%\System.Core.dll" -r:"%FW%\System.Drawing.dll" -r:"%FW%\System.Windows.Forms.dll" -r:"%FW%\System.Web.Extensions.dll" -r:"%FW%\System.Xml.dll" -r:"%FW%\System.IO.Compression.dll" -r:"%FW%\System.IO.Compression.FileSystem.dll" *.cs
if errorlevel 1 exit /b 1
rem Ship the WebView2 assemblies side-by-side with the executable so the app
rem loads them from disk instead of unpacking/loading them in memory at runtime.
if "%~1"=="release" (
 copy /y "%WV%\lib\net462\Microsoft.Web.WebView2.Core.dll" ..\ >nul
 copy /y "%WV%\lib\net462\Microsoft.Web.WebView2.WinForms.dll" ..\ >nul
 if not exist ..\webview2\x64 mkdir ..\webview2\x64
 if not exist ..\webview2\x86 mkdir ..\webview2\x86
 copy /y "%WV%\runtimes\win-x64\native\WebView2Loader.dll" ..\webview2\x64\ >nul
 copy /y "%WV%\runtimes\win-x86\native\WebView2Loader.dll" ..\webview2\x86\ >nul
)
endlocal & exit /b %ERRORLEVEL%
