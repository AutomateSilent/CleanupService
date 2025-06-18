# Scripts Feature - Quick Start Guide

## ✅ Implementation Complete!

The scripts feature has been successfully added to your Kiosk Profile Cleanup Service with these essential files:

### 📁 **Files Added/Modified:**
- ✅ **ScriptRunner.cs** - New script execution engine
- ✅ **CleanupService.cs** - Updated with script integration
- ✅ **App.config** - Added script configuration with examples
- ✅ **CleanupService.csproj** - Updated to include ScriptRunner.cs

---

## 🚀 **Quick Test Setup**

### 1. **Create Scripts Directory**
```batch
mkdir C:\KioskScripts
```

### 2. **Create a Test Script**
Save this as `C:\KioskScripts\TestScript.ps1`:
```powershell
param(
    [string]$SessionEvent = "Unknown",
    [string]$SessionId = "0"
)

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
Write-Output "[$timestamp] TEST SCRIPT: Event=$SessionEvent, Session=$SessionId"

# Example: Create a test file to prove it ran
$testFile = "C:\Logs\ProfileCleanUp\script_test.txt"
Add-Content -Path $testFile -Value "[$timestamp] Script executed for $SessionEvent on session $SessionId"

exit 0
```

### 3. **Enable the Test Script**
In your `App.config`, uncomment and modify:
```xml
<!-- Test Script: Runs on lock/unlock for easy testing -->
<add key="Script1Path" value="C:\KioskScripts\TestScript.ps1" />
<add key="Script1Events" value="Lock,Unlock" />
<add key="Script1TimeoutSeconds" value="30" />
<add key="Script1PassSessionInfo" value="true" />
```

### 4. **Build and Deploy**
1. Build the project in Visual Studio (Build → Build Solution)
2. Stop the service: `sc stop KioskProfileCleanupService`
3. Copy the new .exe to your service directory
4. Start the service: `sc start KioskProfileCleanupService`

### 5. **Test the Feature**
1. Lock your session (Windows+L)
2. Unlock your session
3. Check the logs:
   - Service log: `C:\Logs\ProfileCleanUp\ProfileCleanup.log`
   - Script test file: `C:\Logs\ProfileCleanUp\script_test.txt`

---

## 📋 **Configuration Reference**

### **Session Events Available:**
- **Startup** - Service starts (system boot)
- **Logon** - User logs in
- **Logoff** - User logs out
- **Lock** - Session locked (Windows+L)
- **Unlock** - Session unlocked
- **Resume** - System resumes from sleep
- **Shutdown** - System shutting down
- **AllSessions** - Runs on ALL session events

### **Script Configuration Format:**
```xml
<add key="Script1Path" value="C:\Path\To\Your\Script.ps1" />
<add key="Script1Events" value="Logon,Logoff" />
<add key="Script1TimeoutSeconds" value="60" />
<add key="Script1PassSessionInfo" value="true" />
```

### **What Each Setting Does:**
- **ScriptXPath** - Full path to your .bat or .ps1 file
- **ScriptXEvents** - Comma-separated list of when to run
- **ScriptXTimeoutSeconds** - Max time to wait (optional, default: 60)
- **ScriptXPassSessionInfo** - Pass event/session as parameters (optional, default: false)

---

## 🔍 **Monitoring & Troubleshooting**

### **Check if Scripts are Running:**
```powershell
# Monitor live script activity
Get-Content "C:\Logs\ProfileCleanUp\ProfileCleanup.log" -Wait

# Filter for script entries only
Get-Content "C:\Logs\ProfileCleanUp\ProfileCleanup.log" | Where-Object {$_ -match "ScriptRunner"}
```

### **Expected Log Messages:**
```
[INFO] ScriptRunner: Checking for scripts to run for event 'Lock'
[INFO] ScriptRunner: Found 1 script(s) to execute for event 'Lock'
[INFO] ScriptRunner: Starting execution of 'C:\KioskScripts\TestScript.ps1'
[INFO] ScriptRunner: PowerShell output: [2025-06-18 14:30:15] TEST SCRIPT: Event=Lock, Session=2
[INFO] ScriptRunner: PowerShell script completed successfully (Exit Code: 0)
```

### **Common Issues:**
1. **Script not running** - Check EnableScripts=true and script path exists
2. **Permission denied** - Service runs as SYSTEM, should have full access
3. **Script fails** - Check script syntax and test manually as admin
4. **Timeout** - Increase ScriptXTimeoutSeconds if script needs more time

---

## 🎯 **Real-World Examples**

### **Basic Kiosk Lockdown:**
```xml
<!-- Registry tweaks on all session changes -->
<add key="Script1Path" value="C:\KioskScripts\RegistryLockdown.ps1" />
<add key="Script1Events" value="AllSessions" />

<!-- Start kiosk app on logon -->
<add key="Script2Path" value="C:\KioskScripts\StartKioskApp.bat" />
<add key="Script2Events" value="Logon" />

<!-- Cleanup on logoff -->
<add key="Script3Path" value="C:\KioskScripts\UserCleanup.ps1" />
<add key="Script3Events" value="Logoff" />
```

### **Security Hardening:**
```xml
<!-- Disable USB on startup/logon -->
<add key="Script1Path" value="C:\Security\DisableUSB.ps1" />
<add key="Script1Events" value="Startup,Logon" />

<!-- Network restrictions -->
<add key="Script2Path" value="C:\Security\NetworkLockdown.ps1" />
<add key="Script2Events" value="AllSessions" />
```

---

## ✅ **Next Steps**

1. **Test the basic functionality** with the test script above
2. **Create your real scripts** for kiosk configuration
3. **Monitor the logs** to ensure everything works
4. **Gradually add more scripts** as needed

The scripts feature is now fully integrated and ready to use! It will execute faster than Task Scheduler and provide comprehensive logging through your existing system.
