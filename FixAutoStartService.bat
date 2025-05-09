@echo off
echo Fixing Kiosk Profile Cleanup Service Auto-Start...
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

REM Verify service exists
sc query KioskProfileCleanupService >nul
if %ERRORLEVEL% neq 0 (
    echo ERROR: KioskProfileCleanupService not found.
    echo Please install the service first.
    echo.
    pause
    exit /b 1
)

echo Setting service to automatic startup...
sc config KioskProfileCleanupService start= auto
if %ERRORLEVEL% equ 0 (
    echo Service successfully configured for automatic startup!
) else (
    echo Failed to set automatic startup. Error code: %ERRORLEVEL%
)

echo Enabling service recovery options...
REM Configure service to restart after 1 minute on first failure, 
REM 2 minutes on second failure, and also after subsequent failures
sc failure KioskProfileCleanupService reset= 86400 actions= restart/60000/restart/120000/restart/120000
if %ERRORLEVEL% equ 0 (
    echo Service recovery options set successfully!
) else (
    echo Failed to set service recovery options. Error code: %ERRORLEVEL%
)

echo Starting service...
net start KioskProfileCleanupService
if %ERRORLEVEL% equ 0 (
    echo Service started successfully!
) else (
    echo Failed to start service. Error code: %ERRORLEVEL%
    echo Check the Windows Event Log for more details.
)

echo.
echo Service Status:
sc query KioskProfileCleanupService | findstr STATE

echo.
pause
