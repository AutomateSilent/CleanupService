@echo off
echo Uninstalling Kiosk Profile Cleanup Service...
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

REM Check if service is running and stop it
sc query KioskProfileCleanupService >nul
if %ERRORLEVEL% equ 0 (
    echo Stopping service...
    net stop KioskProfileCleanupService
    
    REM Give it a moment to fully stop
    timeout /t 2 /nobreak >nul
)

REM Check if service executable exists
if not exist "%SERVICE_EXE%" (
    echo WARNING: Service executable not found at: %SERVICE_EXE%
    echo Will attempt to remove service using SC command instead.
    
    sc delete KioskProfileCleanupService
    if %ERRORLEVEL% equ 0 (
        echo Service removed successfully!
    ) else (
        echo Failed to remove service!
    )
) else (
    REM Run the uninstaller
    echo Uninstalling service from: %SERVICE_EXE%
    "%SERVICE_EXE%" -uninstall
)

REM Verify service was removed
sc query KioskProfileCleanupService >nul
if %ERRORLEVEL% neq 0 (
    echo.
    echo Service uninstalled successfully!
) else (
    echo.
    echo Service uninstallation might have encountered issues.
    echo Check the application log for more details.
)

echo.
pause
