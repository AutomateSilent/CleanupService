@echo off
echo Installing Kiosk Profile Cleanup Service...
echo.

REM Check for admin rights
NET SESSION >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: Administrator privileges required!
    echo Please right-click this batch file and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

REM Get current directory
set CURRENT_DIR=%~dp0
set SERVICE_EXE=%CURRENT_DIR%CleanupService.exe

REM Check if service executable exists
if not exist "%SERVICE_EXE%" (
    echo ERROR: Service executable not found at: %SERVICE_EXE%
    echo Please make sure you run this script from the directory containing CleanupService.exe
    echo.
    pause
    exit /b 1
)

REM Run the installer
echo Installing service from: %SERVICE_EXE%
"%SERVICE_EXE%" -install

REM Verify service was installed
sc query KioskProfileCleanupService >nul
if %ERRORLEVEL% equ 0 (
    echo.
    echo Service installed successfully!
    echo Starting service...
    net start KioskProfileCleanupService
) else (
    echo.
    echo Service installation might have encountered issues.
    echo Check the application log for more details.
)

echo.
pause
