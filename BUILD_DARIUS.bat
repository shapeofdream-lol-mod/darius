@echo off
chcp 65001 >nul
setlocal EnableExtensions
set "BUILD_EXIT=1"
cd /d "%~dp0"
set "LAUNCH_LOG=%~dp0LAUNCH_LOG.txt"

> "%LAUNCH_LOG%" echo ==== DariusPrototype launcher started %date% %time% ====
>> "%LAUNCH_LOG%" echo ScriptDir=%~dp0
>> "%LAUNCH_LOG%" echo WorkingDir=%CD%

echo Darius build launcher
echo Working directory: %CD%
echo.

where powershell.exe >> "%LAUNCH_LOG%" 2>&1
if errorlevel 1 (
    echo ERROR: powershell.exe was not found.
    >> "%LAUNCH_LOG%" echo ERROR: powershell.exe was not found.
    goto END
)

echo Starting PowerShell build script...
>> "%LAUNCH_LOG%" echo Starting BuildAndInstall.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildAndInstall.ps1" >> "%LAUNCH_LOG%" 2>&1
set "BUILD_EXIT=%ERRORLEVEL%"
>> "%LAUNCH_LOG%" echo PowerShell exit code: %BUILD_EXIT%

echo.
echo PowerShell exit code: %BUILD_EXIT%
echo.
echo ===== LAUNCH_LOG.txt =====
type "%LAUNCH_LOG%"
echo ==========================
echo.
if exist "%~dp0BUILD_LOG.txt" (
    echo BUILD_LOG.txt was created.
) else (
    echo BUILD_LOG.txt was NOT created. LAUNCH_LOG.txt should contain the startup error.
)

:END
echo.
if /I "%~1"=="--from-all" goto NO_PAUSE
echo The window will stay open now.
echo If anything failed, keep LAUNCH_LOG.txt and BUILD_LOG.txt for troubleshooting.
echo Press any key to close this window.
pause >nul
:NO_PAUSE
endlocal & exit /b %BUILD_EXIT%
