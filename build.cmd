@echo off
setlocal
cd /d "%~dp0"

rem Pass "deploy" to also copy the finished plugin into the host's Plugins folder.
set "DEPLOY="
if /i "%~1"=="deploy" set "DEPLOY=-p:DeployToHost=true"

if defined DEPLOY (
  rem The plugin DLL cannot be replaced while Mapping Tools has it loaded.
  tasklist /FI "IMAGENAME eq Mapping Tools.exe" /NH 2>nul | findstr /I /C:"Mapping Tools.exe" >nul
  if not errorlevel 1 (
    echo Mapping Tools is currently running.
    echo Close it before deploying - the plugin DLL cannot be replaced while it is loaded.
    pause
    exit /b 1
  )
)

echo Building Cross-line slider recalc...
dotnet build CrossLineSliderRecalc\Mapping_Tools.CrossLineSliderRecalc.csproj -c Debug %DEPLOY%
if errorlevel 1 (
  echo.
  echo FAILED.
  pause
  exit /b 1
)

echo.
if defined DEPLOY (
  echo Done. The plugin was copied into the host's Plugins folder; restart Mapping Tools.
) else (
  echo Done. The plugin is at:
  echo   CrossLineSliderRecalc\bin\Debug\plugin\CrossLineSliderRecalc.dll
  echo Copy it into %APPDATA%\Mapping Tools\Plugins and restart Mapping Tools.
)
echo.

rem Keep the window open when this is double-clicked from Explorer.
pause
