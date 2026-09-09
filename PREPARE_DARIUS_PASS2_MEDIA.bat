@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"
set "EXIT_CODE=1"
echo Darius Pass 2 authentic Riot Wwise media preparation
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\PrepareDariusPass2Media.ps1"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if "%EXIT_CODE%"=="0" (
  echo PASS2 MEDIA READY.
) else (
  echo PASS2 MEDIA PREPARATION FAILED. Exit=%EXIT_CODE%
)
if /I "%~1"=="--from-build" goto DONE
pause
:DONE
endlocal & exit /b %EXIT_CODE%
