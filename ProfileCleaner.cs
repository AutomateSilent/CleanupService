using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace CleanupService
{
    public static class ProfileCleaner
    {
        // P/Invoke for shell32.dll to empty the recycle bin
        [DllImport("shell32.dll")]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
        
        // Flags for SHEmptyRecycleBin
        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

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
                
                // Empty all recycle bins (null for pszRootPath means all drives)
                uint flags = SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND;
                int result = SHEmptyRecycleBin(IntPtr.Zero, null, flags);
                
                if (result == 0)
                {
                    Logger.LogInfo("Recycle Bin emptied successfully");
                }
                else
                {
                    Logger.LogWarning($"Failed to empty Recycle Bin. Error code: {result}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error emptying Recycle Bin: {ex.Message}");
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
