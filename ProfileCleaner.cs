using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using System.Threading;

namespace CleanupService
{
    public static class ProfileCleaner
    {
        // Configure cleanup settings
        private static readonly string[] SystemTempFolders = new string[]
        {
            @"C:\WINDOWS\TEMP",
            @"C:\WINDOWS\Prefetch"
        };

        // User temp paths (relative to user profile)
        private static readonly string[] UserTempRelativePaths = new string[]
        {
            @"AppData\Local\Temp",
            @"AppData\Local\Microsoft\Windows\Temporary Internet Files",
            @"AppData\Local\Microsoft\Windows\INetCache",
            @"AppData\Local\Microsoft\Windows\WER",
            @"AppData\Local\CrashDumps"
        };

        // Browser cache paths (relative to user profile)
        private static readonly string[] BrowserCacheRelativePaths = new string[]
        {
            // Chrome
            @"AppData\Local\Google\Chrome\User Data\Default\Cache",
            @"AppData\Local\Google\Chrome\User Data\Default\Media Cache",
            // Edge
            @"AppData\Local\Microsoft\Edge\User Data\Default\Cache",
            // Firefox
            @"AppData\Local\Mozilla\Firefox\Profiles"
        };

        // User folders to clean (relative to user profile)
        private static readonly string[] UserFolderNames = new string[]
        {
            "Desktop",
            "Downloads",
            "Pictures",
            "Videos",
            "Music",
            "Favorites",
            "Contacts",
            "Links",
            "Saved Games",
            "Searches"
        };

        // Document folder needs special handling due to junction points
        private static readonly string DocumentsFolderName = "Documents";

        // Additional important folders to clean
        private static readonly string[] AdditionalUserPaths = new string[]
        {
            @"AppData\Roaming\Microsoft\Windows\Recent"
        };

        // System accounts that should be excluded from cleaning
        private static readonly string[] SystemAccounts = new string[]
        {
            "Administrator",
            "Default",
            "Public",
            "defaultuser0",
            "All Users",
            "Default User",
            "Administrator.DESKTOP",
            "Administrator.WORKGROUP",
            "Administrator.DOMAIN"
        };

        // Known junction points in Documents folder that should be skipped
        private static readonly string[] KnownJunctionPoints = new string[]
        {
            "My Music",
            "My Pictures",
            "My Videos"
        };

        public static void CloseConfiguredProcesses()
        {
            try
            {
                Logger.LogInfo("Closing configured processes from App.config");

                // Get the list of processes to close from config
                string processesString = System.Configuration.ConfigurationManager.AppSettings["ProcessesToClose"];

                if (string.IsNullOrEmpty(processesString))
                {
                    Logger.LogInfo("No processes to close specified in configuration");
                    return;
                }

                // Split the comma-separated list
                string[] processes = processesString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (processes.Length == 0)
                {
                    Logger.LogInfo("No processes to close after splitting configuration");
                    return;
                }

                Logger.LogInfo($"Found {processes.Length} processes to close in configuration");

                // Close Office products if specified
                bool closeOfficeProducts = false;
                foreach (string process in processes)
                {
                    string trimmedProcess = process.Trim().ToLowerInvariant();

                    if (trimmedProcess.Equals("office", StringComparison.OrdinalIgnoreCase))
                    {
                        closeOfficeProducts = true;
                        break;
                    }
                }

                if (closeOfficeProducts)
                {
                    CloseOfficeProcesses();
                }

                // Close each specific process
                foreach (string process in processes)
                {
                    string trimmedProcess = process.Trim();

                    // Skip "office" as we've already handled it
                    if (trimmedProcess.Equals("office", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        Logger.LogInfo($"Closing process: {trimmedProcess}");

                        Process[] runningProcesses = Process.GetProcessesByName(trimmedProcess);

                        if (runningProcesses.Length == 0)
                        {
                            Logger.LogInfo($"No instances of {trimmedProcess} found running");
                            continue;
                        }

                        Logger.LogInfo($"Found {runningProcesses.Length} instances of {trimmedProcess} running");

                        foreach (Process proc in runningProcesses)
                        {
                            try
                            {
                                proc.CloseMainWindow();

                                // Give it a moment to close gracefully
                                if (!proc.WaitForExit(3000))
                                {
                                    // If the process hasn't exited after waiting, force kill it
                                    Logger.LogInfo($"Process {proc.ProcessName} (ID: {proc.Id}) not responding to close request, force killing");
                                    proc.Kill();
                                }

                                Logger.LogInfo($"Successfully closed {proc.ProcessName} (ID: {proc.Id})");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error closing process {proc.ProcessName} (ID: {proc.Id}): {ex.Message}");
                            }
                            finally
                            {
                                proc.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error processing configured process {trimmedProcess}: {ex.Message}");
                    }
                }

                Logger.LogInfo("Finished closing configured processes");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in CloseConfiguredProcesses: {ex.Message}");
            }
        }


        public static void RestartExplorer()
        {
            try
            {
                Logger.LogInfo("Attempting to restart Windows Explorer");

                //// Check if we should restart Explorer based on configuration
                //string restartExplorerSetting = System.Configuration.ConfigurationManager.AppSettings["RestartExplorerOnSessionChange"];

                //// Parse the setting value, default to true if missing or invalid
                //bool shouldRestart = true;
                //if (!string.IsNullOrEmpty(restartExplorerSetting))
                //{
                //    bool.TryParse(restartExplorerSetting, out shouldRestart);
                //}

                //if (!shouldRestart)
                //{
                //    Logger.LogInfo("Explorer restart disabled in configuration");
                //    return;
                //}

                // Find all explorer processes
                Process[] explorerProcesses = Process.GetProcessesByName("explorer");

                if (explorerProcesses.Length == 0)
                {
                    Logger.LogInfo("No explorer.exe processes found");
                    return;
                }

                // Kill all explorer instances
                Logger.LogInfo($"Found {explorerProcesses.Length} explorer.exe processes, terminating them");

                foreach (Process process in explorerProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(2000);
                        Logger.LogInfo($"Terminated explorer.exe process (ID: {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error terminating explorer.exe process (ID: {process.Id}): {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                // Wait a bit for cleanup
                Thread.Sleep(1000);

            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in RestartExplorer: {ex.Message}");
            }
        }

        // This method closes all configured processes from App.config &  Microsoft Office applications
        private static void CloseOfficeProcesses()
        {
            Logger.LogInfo("Closing Microsoft Office applications");

            // List of common Office application process names
            string[] officeProcessNames = new string[]
            {
        "WINWORD",    // Microsoft Word
        "EXCEL",      // Microsoft Excel
        "POWERPNT",   // Microsoft PowerPoint
        "OUTLOOK",    // Microsoft Outlook
        "ONENOTE",    // Microsoft OneNote
        "MSACCESS",   // Microsoft Access
        "MSPUB",      // Microsoft Publisher
        "VISIO",      // Microsoft Visio
        "PROJECTPRO", // Microsoft Project
        "GROOVE",     // Microsoft SharePoint Workspace
        "INFOPATH",   // Microsoft InfoPath
        "ONENOTEM",   // Microsoft OneNote Quick Launcher
        "LYNC",       // Microsoft Lync
        "SKYPE",      // Skype for Business
        "TEAMS",      // Microsoft Teams
        "MSTORE",      // Microsoft Store Office apps
        "edge",      // Microsoft Edge (if used for Office 365)
        "msedge" // Microsoft Edge (if used for Office 365)
            };

            foreach (string processName in officeProcessNames)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(processName);

                    if (processes.Length == 0)
                    {
                        continue;
                    }

                    Logger.LogInfo($"Found {processes.Length} instances of {processName} running");

                    foreach (Process proc in processes)
                    {
                        try
                        {
                            // Try to close gracefully first
                            proc.CloseMainWindow();

                            // Give the application time to save and close
                            if (!proc.WaitForExit(2000))
                            {
                                // If it hasn't closed after 2 seconds, force it to close
                                Logger.LogInfo($"{processName} not responding to close request, force killing");
                                proc.Kill();
                            }

                            Logger.LogInfo($"Successfully closed {processName} (ID: {proc.Id})");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Error closing {processName} (ID: {proc.Id}): {ex.Message}");
                        }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error processing Office application {processName}: {ex.Message}");
                }
            }

            Logger.LogInfo("Finished closing Microsoft Office applications");
        }


        // Main cleanup method
        public static void RunCleanup(string triggerSource)
        {
            Logger.LogInfo($"Starting cleanup process. Trigger: {triggerSource}");

            // Check for SYSTEM/admin permissions
            bool isElevated = CheckPermissions();
            Logger.LogInfo($"Running with elevated permissions: {isElevated}");

            // Perform different cleanup operations based on the trigger
            switch (triggerSource.ToLower())
            {
                case "system startup":
                    PerformStartupCleanup();
                    break;

                case "user logon":
                    PerformLogonCleanup();
                    break;

                case "user logoff":
                    PerformLogoffCleanup();
                    break;

                case "resume from sleep":
                    // Lighter cleanup for resume events
                    CleanSystemTempFolders();
                    break;

                case "system shutdown":
                    // Quick cleanup for shutdown events
                    CleanAllUserBrowserCaches();
                    break;

                default:
                    // Default cleanup for manual runs or other triggers
                    PerformFullCleanup();
                    break;
            }

            Logger.LogInfo("Cleanup process completed");
        }

        private static bool CheckPermissions()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            bool isSystem = identity.User.Equals(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));

            return isAdmin || isSystem;
        }

        private static void PerformStartupCleanup()
        {
            Logger.LogInfo("Executing startup cleanup tasks");
            
            // Clear all system temp files
            CleanSystemTempFolders();
            
            // Clean all user files
            CleanAllUserProfiles(false);
            
            // Empty the recycle bin
            EmptyRecycleBin();
        }

        private static void PerformLogonCleanup()
        {
            Logger.LogInfo("Executing logon cleanup tasks");
            
            // Clean all user files
            CleanAllUserProfiles(false);
            
            // Empty the recycle bin
            EmptyRecycleBin();
        }

        private static void PerformLogoffCleanup()
        {
            Logger.LogInfo("Executing logoff cleanup tasks");
            
            // Clean all user files with thorough cleaning
            CleanAllUserProfiles(true);
            
            // Empty the recycle bin
            EmptyRecycleBin();
        }

        private static void PerformFullCleanup()
        {
            Logger.LogInfo("Executing full cleanup (manual mode)");
            
            // Clean system temp folders
            CleanSystemTempFolders();
            
            // Clean all user profiles thoroughly
            CleanAllUserProfiles(true);
            
            // Empty the recycle bin
            EmptyRecycleBin();
        }

        #region Cleanup Operations

        private static void EmptyRecycleBin()
        {
            try
            {
                Logger.LogInfo("Emptying the Recycle Bin for all drives");

                // Method 1: Try using command line (most reliable method from a service)
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "cmd.exe";
                    psi.Arguments = "/c rd /s /q C:\\$Recycle.Bin";
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;

                    Logger.LogInfo("Executing command: cmd.exe /c rd /s /q C:\\$Recycle.Bin");
                    Process process = Process.Start(psi);
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        Logger.LogInfo($"Command output: {output}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        Logger.LogWarning($"Command error: {error}");
                    }
                    
                    Logger.LogInfo($"Recycle Bin emptied with exit code: {process.ExitCode}");

                    // Check other drives too
                    foreach (DriveInfo drive in DriveInfo.GetDrives())
                    {
                        if (drive.DriveType == DriveType.Fixed && 
                            drive.IsReady && 
                            drive.RootDirectory.FullName != "C:\\")
                        {
                            string recyclePath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                            
                            psi.Arguments = $"/c rd /s /q {recyclePath}";
                            Logger.LogInfo($"Executing command: cmd.exe {psi.Arguments}");
                            
                            process = Process.Start(psi);
                            process.WaitForExit();
                            
                            Logger.LogInfo($"Recycle Bin emptied on drive {drive.Name} with exit code: {process.ExitCode}");
                        }
                    }

                    Logger.LogInfo("Recycle Bin emptied successfully using command line");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error emptying Recycle Bin using command line: {ex.Message}");
                    
                    // Method 2: Try using direct folder deletion as backup
                    try
                    {
                        Logger.LogInfo("Attempting alternative method to empty Recycle Bin");
                        
                        // Get all drives
                        foreach (DriveInfo drive in DriveInfo.GetDrives())
                        {
                            if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                            {
                                string recycleBinPath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                                
                                if (Directory.Exists(recycleBinPath))
                                {
                                    Logger.LogInfo($"Cleaning Recycle Bin at: {recycleBinPath}");
                                    
                                    // Get all subdirectories (each user's recycle bin)
                                    string[] userBins = Directory.GetDirectories(recycleBinPath);
                                    
                                    foreach (string userBin in userBins)
                                    {
                                        try
                                        {
                                            Logger.LogInfo($"Cleaning user recycle bin: {userBin}");
                                            
                                            // Instead of trying to delete the whole folder, delete each file individually
                                            DeleteRecursive(userBin);
                                            
                                            Logger.LogInfo($"Cleaned user recycle bin: {userBin}");
                                        }
                                        catch (Exception userEx)
                                        {
                                            Logger.LogWarning($"Error cleaning user recycle bin {userBin}: {userEx.Message}");
                                        }
                                    }
                                }
                            }
                        }
                        
                        Logger.LogInfo("Recycle Bin emptied successfully using direct folder deletion");
                    }
                    catch (Exception altEx)
                    {
                        Logger.LogError($"Error emptying Recycle Bin using alternative method: {altEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in EmptyRecycleBin: {ex.Message}");
            }
        }

        private static void DeleteRecursive(string path)
        {
            // First, delete all files in this directory
            try
            {
                string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                
                foreach (string file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not delete file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error getting files in {path}: {ex.Message}");
            }
            
            // Then, recursively clean all subdirectories
            try
            {
                string[] dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
                
                foreach (string dir in dirs)
                {
                    try
                    {
                        DeleteRecursive(dir);
                        
                        // Try to remove the now-empty directory
                        Directory.Delete(dir, false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not process directory {dir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error getting subdirectories in {path}: {ex.Message}");
            }
        }

        private static void CleanSystemTempFolders()
        {
            Logger.LogInfo("Cleaning system temporary folders");
            
            foreach (string tempFolder in SystemTempFolders)
            {
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        Logger.LogInfo($"Cleaning system temp folder: {tempFolder}");
                        int filesRemoved = DeleteFilesInFolder(tempFolder, "*.*", SearchOption.AllDirectories);
                        Logger.LogInfo($"Removed {filesRemoved} files from system temp folder");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error cleaning system temp folder {tempFolder}: {ex.Message}");
                }
            }
        }

        private static void CleanAllUserProfiles(bool thorough)
        {
            // Get all user profiles
            List<string> userProfiles = GetAllUserProfiles();
            Logger.LogInfo($"Found {userProfiles.Count} user profiles to clean");
            
            foreach (string userProfile in userProfiles)
            {
                string userName = Path.GetFileName(userProfile);
                Logger.LogInfo($"Cleaning user profile: {userName}");
                
                // Clean user temp folders
                CleanUserTempFolders(userProfile);
                
                // Clean browser caches
                CleanUserBrowserCache(userProfile);
                
                // Clean user folders
                CleanUserFolders(userProfile, thorough);
                
                // Special handling for Documents folder
                CleanDocumentsFolder(userProfile);
            }
        }

        private static void CleanUserTempFolders(string userProfilePath)
        {
            Logger.LogInfo($"Cleaning temporary folders for user: {Path.GetFileName(userProfilePath)}");
            
            foreach (string relativePath in UserTempRelativePaths)
            {
                try
                {
                    string fullPath = Path.Combine(userProfilePath, relativePath);
                    
                    if (Directory.Exists(fullPath))
                    {
                        Logger.LogInfo($"Cleaning user temp folder: {fullPath}");
                        int filesRemoved = DeleteFilesInFolder(fullPath, "*.*", SearchOption.AllDirectories);
                        Logger.LogInfo($"Removed {filesRemoved} files from {Path.GetFileName(relativePath)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error cleaning user temp folder {relativePath}: {ex.Message}");
                }
            }
        }

        private static void CleanAllUserBrowserCaches()
        {
            List<string> userProfiles = GetAllUserProfiles();
            Logger.LogInfo($"Cleaning browser caches for {userProfiles.Count} user profiles");
            
            foreach (string userProfile in userProfiles)
            {
                CleanUserBrowserCache(userProfile);
            }
        }

        private static void CleanUserBrowserCache(string userProfilePath)
        {
            Logger.LogInfo($"Cleaning browser cache for user: {Path.GetFileName(userProfilePath)}");
            
            foreach (string relativePath in BrowserCacheRelativePaths)
            {
                try
                {
                    string fullPath = Path.Combine(userProfilePath, relativePath);
                    
                    if (Directory.Exists(fullPath))
                    {
                        Logger.LogInfo($"Cleaning browser cache: {fullPath}");
                        int filesRemoved = DeleteFilesInFolder(fullPath, "*.*", SearchOption.AllDirectories);
                        Logger.LogInfo($"Removed {filesRemoved} files from browser cache");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error cleaning browser cache {relativePath}: {ex.Message}");
                }
            }
        }

        private static void CleanUserFolders(string userProfilePath, bool thorough)
        {
            string userName = Path.GetFileName(userProfilePath);
            Logger.LogInfo($"Cleaning user folders for: {userName}");
            
            // Clean main user folders (Desktop, Downloads, etc.)
            foreach (string folderName in UserFolderNames)
            {
                try
                {
                    string fullPath = Path.Combine(userProfilePath, folderName);
                    
                    if (Directory.Exists(fullPath))
                    {
                        // Different approach for different folders
                        if (folderName.Equals("Desktop", StringComparison.OrdinalIgnoreCase) ||
                            folderName.Equals("Downloads", StringComparison.OrdinalIgnoreCase))
                        {
                            // Always clean important folders
                            Logger.LogInfo($"Cleaning important folder: {fullPath}");
                            int filesRemoved = DeleteFilesInFolder(fullPath, "*.*", SearchOption.AllDirectories);
                            Logger.LogInfo($"Removed {filesRemoved} files from {folderName}");
                        }
                        else if (thorough)
                        {
                            // Only clean other folders in thorough mode
                            Logger.LogInfo($"Thorough cleaning of folder: {fullPath}");
                            int filesRemoved = DeleteFilesInFolder(fullPath, "*.*", SearchOption.AllDirectories);
                            Logger.LogInfo($"Removed {filesRemoved} files from {folderName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error cleaning user folder {folderName}: {ex.Message}");
                }
            }
            
            // Clean additional paths
            foreach (string relativePath in AdditionalUserPaths)
            {
                try
                {
                    string fullPath = Path.Combine(userProfilePath, relativePath);
                    
                    if (Directory.Exists(fullPath))
                    {
                        Logger.LogInfo($"Cleaning additional path: {fullPath}");
                        int filesRemoved = DeleteFilesInFolder(fullPath, "*.*", SearchOption.AllDirectories);
                        Logger.LogInfo($"Removed {filesRemoved} files from {Path.GetFileName(relativePath)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error cleaning additional path {relativePath}: {ex.Message}");
                }
            }
        }

        private static void CleanDocumentsFolder(string userProfilePath)
        {
            try
            {
                string documentsPath = Path.Combine(userProfilePath, DocumentsFolderName);
                
                if (!Directory.Exists(documentsPath))
                {
                    return;
                }
                
                Logger.LogInfo($"Special cleaning of Documents folder: {documentsPath}");

                // Handle only the root level files first
                int rootFilesRemoved = DeleteFilesInFolder(documentsPath, "*.*", SearchOption.TopDirectoryOnly);
                Logger.LogInfo($"Removed {rootFilesRemoved} files from root of Documents folder");
                
                // Now handle each subdirectory individually, skipping known junction points
                foreach (string subdirPath in Directory.GetDirectories(documentsPath))
                {
                    string subdirName = Path.GetFileName(subdirPath);
                    
                    // Skip known junction points that cause problems
                    if (KnownJunctionPoints.Contains(subdirName, StringComparer.OrdinalIgnoreCase))
                    {
                        Logger.LogInfo($"Skipping junction point: {subdirPath}");
                        continue;
                    }
                    
                    try
                    {
                        // Check if directory is a junction/symlink before trying to clean it
                        if (IsReparsePoint(subdirPath))
                        {
                            Logger.LogInfo($"Skipping junction point/symlink: {subdirPath}");
                            continue;
                        }
                        
                        Logger.LogInfo($"Cleaning Documents subfolder: {subdirPath}");
                        int filesRemoved = DeleteFilesInFolder(subdirPath, "*.*", SearchOption.AllDirectories);
                        Logger.LogInfo($"Removed {filesRemoved} files from {subdirName}");
                        
                        // Try to remove the directory if empty
                        if (IsDirectoryEmpty(subdirPath))
                        {
                            try
                            {
                                Directory.Delete(subdirPath, true);
                                Logger.LogInfo($"Removed empty Documents subfolder: {subdirPath}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"Could not remove Directory {subdirPath}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error cleaning Documents subfolder {subdirPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error cleaning Documents folder for profile {userProfilePath}: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private static List<string> GetAllUserProfiles()
        {
            List<string> userProfiles = new List<string>();
            
            try
            {
                string usersPath = @"C:\Users";
                
                if (Directory.Exists(usersPath))
                {
                    string[] directories = Directory.GetDirectories(usersPath);
                    
                    foreach (string directory in directories)
                    {
                        string userName = Path.GetFileName(directory);
                        
                        // Skip system accounts
                        if (!IsSystemAccount(userName))
                        {
                            // Check if this is a real user profile
                            if (IsRealUserProfile(directory))
                            {
                                userProfiles.Add(directory);
                                Logger.LogInfo($"Found user profile: {userName} at {directory}");
                            }
                            else
                            {
                                Logger.LogInfo($"Skipping incomplete profile: {userName}");
                            }
                        }
                        else
                        {
                            Logger.LogInfo($"Skipping system account: {userName}");
                        }
                    }
                }
                else
                {
                    Logger.LogError($"Users directory not found at {usersPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error enumerating user profiles: {ex.Message}");
            }
            
            return userProfiles;
        }

        private static bool IsSystemAccount(string accountName)
        {
            return SystemAccounts.Contains(accountName, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsRealUserProfile(string profilePath)
        {
            try
            {
                // Check for key folders that would indicate this is a real user profile
                string desktopPath = Path.Combine(profilePath, "Desktop");
                string documentsPath = Path.Combine(profilePath, "Documents");
                string appDataPath = Path.Combine(profilePath, "AppData");
                
                bool isReal = Directory.Exists(desktopPath) && 
                              Directory.Exists(documentsPath) && 
                              Directory.Exists(appDataPath);
                
                if (isReal)
                {
                    Logger.LogInfo($"Verified real user profile: {profilePath}");
                }
                else
                {
                    Logger.LogInfo($"Not a complete user profile: {profilePath} (Desktop: {Directory.Exists(desktopPath)}, Documents: {Directory.Exists(documentsPath)}, AppData: {Directory.Exists(appDataPath)})");
                }
                
                return isReal;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking if profile is real: {profilePath}, Error: {ex.Message}");
                return false;
            }
        }

        private static int DeleteFilesInFolder(string folderPath, string searchPattern, SearchOption searchOption)
        {
            int filesDeleted = 0;
            
            try
            {
                // Make sure the directory exists
                if (!Directory.Exists(folderPath))
                {
                    Logger.LogWarning($"Directory not found: {folderPath}");
                    return 0;
                }
                
                // Get all files matching the pattern
                string[] files;
                try
                {
                    files = Directory.GetFiles(folderPath, searchPattern, searchOption);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error getting files in {folderPath}: {ex.Message}");
                    return 0;
                }
                
                Logger.LogInfo($"Found {files.Length} files to delete in {folderPath}");
                
                foreach (string file in files)
                {
                    try
                    {
                        // Skip files that are in use or locked
                        FileInfo fileInfo = new FileInfo(file);
                        if (IsFileLocked(fileInfo))
                        {
                            Logger.LogInfo($"Skipping locked file: {file}");
                            continue;
                        }
                        
                        // Clear readonly attribute if present
                        if ((File.GetAttributes(file) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                        }
                        
                        // Delete the file
                        File.Delete(file);
                        filesDeleted++;
                        
                        // Log every 50 files to avoid log spam
                        if (filesDeleted % 50 == 0)
                        {
                            Logger.LogInfo($"Deleted {filesDeleted} files so far from {folderPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue with other files
                        Logger.LogWarning($"Could not delete file {file}: {ex.Message}");
                    }
                }
                
                // Try to delete empty subdirectories if we're only looking at top level
                if (searchOption == SearchOption.TopDirectoryOnly)
                {
                    try
                    {
                        string[] directories = Directory.GetDirectories(folderPath);
                        foreach (string directory in directories)
                        {
                            try
                            {
                                // Skip system folders like junction points
                                if (IsReparsePoint(directory))
                                {
                                    continue;
                                }
                                
                                // Check if directory is empty (or contains only empty directories)
                                if (IsDirectoryEmpty(directory))
                                {
                                    Logger.LogInfo($"Deleting empty directory: {directory}");
                                    Directory.Delete(directory, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"Could not delete directory {directory}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error checking for empty directories in {folderPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in DeleteFilesInFolder for {folderPath}: {ex.Message}");
            }
            
            return filesDeleted;
        }

        private static bool IsDirectoryEmpty(string path)
        {
            try
            {
                // Check for files
                string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    return false;
                }
                
                // Check subdirectories
                bool isEmpty = true;
                string[] directories = Directory.GetDirectories(path);
                foreach (string dir in directories)
                {
                    if (!IsDirectoryEmpty(dir))
                    {
                        isEmpty = false;
                        break;
                    }
                }
                
                return isEmpty;
            }
            catch
            {
                // If we can't access it, don't try to delete it
                return false;
            }
        }

        private static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (Exception)
            {
                // File is locked or access denied
                return true;
            }
            
            // File is not locked
            return false;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                return (attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
