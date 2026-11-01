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
dotnet "%SDK%\Roslyn\bincore\csc.dll" -nologo -deterministic+ -define:%DefineConstants% -target:winexe -optimize+ -langversion:latest -nostdlib -platform:anycpu -win32icon:app.ico -resource:app-icon.png,NetProbe.AppIcon.png -resource:WebUi\interface.html,RouteX.Browser.Interface.html -resource:..\docs\licenses\WebView2-LICENSE.txt,RouteX.Licenses.WebView2.txt -resource:..\docs\licenses\WebView2-NOTICE.txt,RouteX.Licenses.WebView2Notice.txt -resource:..\docs\licenses\Lucide-LICENSE.txt,RouteX.Licenses.Lucide.txt -resource:"%WV%\lib\net462\Microsoft.Web.WebView2.Core.dll",RouteX.Browser.Microsoft.Web.WebView2.Core.dll -resource:"%WV%\lib\net462\Microsoft.Web.WebView2.WinForms.dll",RouteX.Browser.Microsoft.Web.WebView2.WinForms.dll -resource:"%WV%\runtimes\win-x64\native\WebView2Loader.dll",RouteX.Browser.Loader.x64 -resource:"%WV%\runtimes\win-x86\native\WebView2Loader.dll",RouteX.Browser.Loader.x86 -win32manifest:%PreviewManifest% -out:%PreviewOutput% -r:"%WV%\lib\net462\Microsoft.Web.WebView2.Core.dll" -r:"%WV%\lib\net462\Microsoft.Web.WebView2.WinForms.dll" -r:"%FW%\mscorlib.dll" -r:"%FW%\System.dll" -r:"%FW%\System.Core.dll" -r:"%FW%\System.Drawing.dll" -r:"%FW%\System.Windows.Forms.dll" -r:"%FW%\System.Web.Extensions.dll" -r:"%FW%\System.Xml.dll" -r:"%FW%\System.IO.Compression.dll" -r:"%FW%\System.IO.Compression.FileSystem.dll" *.cs
endlocal & exit /b %ERRORLEVEL%
