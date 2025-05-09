using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace CleanupService
{
    public static class Logger
    {
        private const string LogFolderPath = @"C:\Logs\ProfileCleanUp";
        private const string LogFileName = "ProfileCleanup.log";
        private const int MaxLogSizeBytes = 30 * 1024; // 30KB
        
        private static string LogFilePath => Path.Combine(LogFolderPath, LogFileName);
        private static readonly object LogLock = new object();
        private static bool _initialized = false;

        public static void Initialize()
        {
            try
            {
                // Create log directory if it doesn't exist
                if (!Directory.Exists(LogFolderPath))
                {
                    Directory.CreateDirectory(LogFolderPath);
                    LogInfo("Created log directory: " + LogFolderPath);
                }
                
                // Create log file if it doesn't exist
                if (!File.Exists(LogFilePath))
                {
                    using (FileStream fs = File.Create(LogFilePath))
                    {
                        string header = $"=== Profile Cleanup Service Log Started at {DateTime.Now} ===\r\n";
                        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                        fs.Write(headerBytes, 0, headerBytes.Length);
                    }
                }
                
                // Check log file size and clean if needed
                CheckAndCleanLogFile();
                
                _initialized = true;
            }
            catch (Exception ex)
            {
                // If we can't even initialize logging, write to event log as fallback
                try
                {
                    EventLog.WriteEntry("KioskProfileCleanupService", 
                        $"Failed to initialize log file: {ex.Message}", 
                        EventLogEntryType.Error);
                }
                catch
                {
                    // If even the event log fails, we can't do much else
                }
            }
        }

        public static void LogInfo(string message)
        {
            WriteToLog("INFO", message);
        }

        public static void LogWarning(string message)
        {
            WriteToLog("WARNING", message);
        }

        public static void LogError(string message)
        {
            WriteToLog("ERROR", message);
        }

        private static void WriteToLog(string level, string message)
        {
            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                lock (LogLock) // Ensure thread safety
                {
                    // Check log file size before writing
                    CheckAndCleanLogFile();
                    
                    // Format log entry
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{Thread.CurrentThread.ManagedThreadId}] {message}\r\n";
                    
                    // Append to log file
                    File.AppendAllText(LogFilePath, logEntry);
                    
                    // If in console/debugging mode, also output to console
                    if (Debugger.IsAttached || Environment.UserInteractive)
                    {
                        Console.WriteLine($"[{level}] {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Try to write to event log if file logging fails
                try
                {
                    EventLog.WriteEntry("KioskProfileCleanupService", 
                        $"Failed to write to log file: {ex.Message}. Original message: {message}", 
                        EventLogEntryType.Error);
                }
                catch
                {
                    // If even the event log fails, we can't do much more
                }
            }
        }

        private static void CheckAndCleanLogFile()
        {
            try
            {
                FileInfo logFileInfo = new FileInfo(LogFilePath);
                
                if (logFileInfo.Exists && logFileInfo.Length > MaxLogSizeBytes)
                {
                    // Create a backup of the old log
                    string backupPath = $"{LogFilePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                    
                    // Try to move the file to backup
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath); // Remove existing backup if it exists
                    }
                    
                    File.Move(LogFilePath, backupPath);
                    
                    // Create a new log file
                    using (FileStream fs = File.Create(LogFilePath))
                    {
                        string header = $"=== Log file cleaned at {DateTime.Now} (previous log size: {logFileInfo.Length} bytes) ===\r\n";
                        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                        fs.Write(headerBytes, 0, headerBytes.Length);
                    }
                    
                    // Cleanup old backup files to prevent disk fill
                    CleanOldLogBackups();
                }
            }
            catch (Exception ex)
            {
                // Log to event log if cleaning fails
                try
                {
                    EventLog.WriteEntry("KioskProfileCleanupService", 
                        $"Failed to clean log file: {ex.Message}", 
                        EventLogEntryType.Error);
                }
                catch
                {
                    // If even the event log fails, we can't do much more
                }
            }
        }

        private static void CleanOldLogBackups()
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(LogFolderPath);
                FileInfo[] backupFiles = di.GetFiles("*.bak");
                
                // Keep only the 5 most recent backup files
                if (backupFiles.Length > 5)
                {
                    // Sort by creation time (oldest first)
                    Array.Sort(backupFiles, (x, y) => 
                        DateTime.Compare(x.CreationTime, y.CreationTime));
                    
                    // Delete all but the 5 most recent
                    for (int i = 0; i < backupFiles.Length - 5; i++)
                    {
                        backupFiles[i].Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log to event log if cleanup fails
                try
                {
                    EventLog.WriteEntry("KioskProfileCleanupService", 
                        $"Failed to clean old log backups: {ex.Message}", 
                        EventLogEntryType.Error);
                }
                catch
                {
                    // If even the event log fails, we can't do much more
                }
            }
        }
    }
}
