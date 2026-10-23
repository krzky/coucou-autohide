@echo off
rem Builds the helper, embeds it in the installer, and copies both to dist\
setlocal
dotnet build src\app -c Release --nologo -v q
if errorlevel 1 exit /b 1
if not exist src\setup\payload mkdir src\setup\payload
copy /y src\app\bin\Release\net48\CoucouAutoHide.exe src\setup\payload\CoucouAutoHide.exe >nul
if errorlevel 1 exit /b 1
dotnet build src\setup -c Release --nologo -v q
if errorlevel 1 exit /b 1
if not exist dist mkdir dist
copy /y src\app\bin\Release\net48\CoucouAutoHide.exe dist\CoucouAutoHide.exe >nul
copy /y src\setup\bin\Release\net48\CoucouAutoHideSetup.exe dist\CoucouAutoHideSetup.exe >nul
echo.
echo built:
dir /b dist
