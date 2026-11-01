@echo off
rem Build the production WebView2 interface with the live C# VPN backend.
call "%~dp0build-web-preview.cmd" release
exit /b %ERRORLEVEL%
