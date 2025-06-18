using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CleanupService
{
    /// <summary>
    /// Handles execution of scripts on session change events.
    /// Supports .bat and .ps1 files with configurable triggers.
    /// </summary>
    public static class ScriptRunner
    {
        // Session event types that can trigger scripts
        public enum SessionEvent
        {
            Startup,
            Logon,
            Logoff,
            Lock,
            Unlock,
            Resume,
            Shutdown,
            AllSessions
        }

        /// <summary>
        /// Execute scripts configured for a specific session event
        /// </summary>
        public static void RunScriptsForEvent(SessionEvent sessionEvent, int? sessionId = null)
        {
            try
            {
                Logger.LogInfo($"ScriptRunner: Checking for scripts to run for event '{sessionEvent}' (Session: {sessionId?.ToString() ?? "N/A"})");

                // Check if scripts feature is enabled
                if (!IsScriptsFeatureEnabled())
                {
                    Logger.LogInfo("ScriptRunner: Scripts feature is disabled in configuration");
                    return;
                }

                // Get scripts configured for this event
                List<ScriptConfig> scriptsToRun = GetScriptsForEvent(sessionEvent);

                if (scriptsToRun.Count == 0)
                {
                    Logger.LogInfo($"ScriptRunner: No scripts configured for event '{sessionEvent}'");
                    return;
                }

                Logger.LogInfo($"ScriptRunner: Found {scriptsToRun.Count} script(s) to execute for event '{sessionEvent}'");

                // Execute each script in a separate thread
                foreach (ScriptConfig script in scriptsToRun)
                {
                    ScriptConfig scriptToExecute = script;
                    
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            ExecuteScript(scriptToExecute, sessionEvent, sessionId);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"ScriptRunner: Error in script execution thread for '{scriptToExecute.Path}': {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error in RunScriptsForEvent for '{sessionEvent}': {ex.Message}");
            }
        }

        /// <summary>
        /// Execute a single script
        /// </summary>
        private static void ExecuteScript(ScriptConfig script, SessionEvent sessionEvent, int? sessionId)
        {
            try
            {
                Logger.LogInfo($"ScriptRunner: Starting execution of '{script.Path}' for event '{sessionEvent}'");

                // Verify script file exists
                if (!File.Exists(script.Path))
                {
                    Logger.LogError($"ScriptRunner: Script file not found: '{script.Path}'");
                    return;
                }

                // Determine script type and execute
                string extension = Path.GetExtension(script.Path).ToLowerInvariant();
                bool success = false;

                switch (extension)
                {
                    case ".ps1":
                        success = ExecutePowerShellScript(script, sessionEvent, sessionId);
                        break;
                    case ".bat":
                    case ".cmd":
                        success = ExecuteBatchScript(script, sessionEvent, sessionId);
                        break;
                    default:
                        Logger.LogError($"ScriptRunner: Unsupported script type '{extension}' for file '{script.Path}'");
                        return;
                }

                if (success)
                {
                    Logger.LogInfo($"ScriptRunner: Successfully executed '{script.Path}' for event '{sessionEvent}'");
                }
                else
                {
                    Logger.LogError($"ScriptRunner: Failed to execute '{script.Path}' for event '{sessionEvent}'");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error executing script '{script.Path}': {ex.Message}");
            }
        }

        /// <summary>
        /// Execute a PowerShell script with bypass execution policy
        /// </summary>
        private static bool ExecutePowerShellScript(ScriptConfig script, SessionEvent sessionEvent, int? sessionId)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -NonInteractive -NoProfile -File \"{script.Path}\"";
                
                // Add session info as parameters if configured
                if (script.PassSessionInfo)
                {
                    psi.Arguments += $" -SessionEvent \"{sessionEvent}\" -SessionId \"{sessionId?.ToString() ?? "0"}\"";
                }

                // Configure hidden execution
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WorkingDirectory = Path.GetDirectoryName(script.Path);

                Logger.LogInfo($"ScriptRunner: Executing PowerShell: {psi.Arguments}");

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    bool completed = process.WaitForExit(script.TimeoutSeconds * 1000);

                    if (!completed)
                    {
                        Logger.LogError($"ScriptRunner: PowerShell script '{script.Path}' timed out after {script.TimeoutSeconds} seconds");
                        try { process.Kill(); } catch { }
                        return false;
                    }

                    // Log output if present
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Logger.LogInfo($"ScriptRunner: PowerShell output: {output.Trim()}");
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Logger.LogWarning($"ScriptRunner: PowerShell errors: {error.Trim()}");
                    }

                    if (process.ExitCode == 0)
                    {
                        Logger.LogInfo($"ScriptRunner: PowerShell script completed successfully (Exit Code: 0)");
                        return true;
                    }
                    else
                    {
                        Logger.LogError($"ScriptRunner: PowerShell script failed with exit code: {process.ExitCode}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error executing PowerShell script '{script.Path}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute a batch script
        /// </summary>
        private static bool ExecuteBatchScript(ScriptConfig script, SessionEvent sessionEvent, int? sessionId)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c \"\"{script.Path}\"\"";
                
                // Add session info as environment variables
                psi.EnvironmentVariables["KIOSK_SESSION_EVENT"] = sessionEvent.ToString();
                psi.EnvironmentVariables["KIOSK_SESSION_ID"] = sessionId?.ToString() ?? "0";

                // Configure hidden execution
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WorkingDirectory = Path.GetDirectoryName(script.Path);

                Logger.LogInfo($"ScriptRunner: Executing batch: {psi.Arguments}");

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    bool completed = process.WaitForExit(script.TimeoutSeconds * 1000);

                    if (!completed)
                    {
                        Logger.LogError($"ScriptRunner: Batch script '{script.Path}' timed out after {script.TimeoutSeconds} seconds");
                        try { process.Kill(); } catch { }
                        return false;
                    }

                    // Log output if present
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Logger.LogInfo($"ScriptRunner: Batch output: {output.Trim()}");
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Logger.LogWarning($"ScriptRunner: Batch errors: {error.Trim()}");
                    }

                    if (process.ExitCode == 0)
                    {
                        Logger.LogInfo($"ScriptRunner: Batch script completed successfully (Exit Code: 0)");
                        return true;
                    }
                    else
                    {
                        Logger.LogError($"ScriptRunner: Batch script failed with exit code: {process.ExitCode}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error executing batch script '{script.Path}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if the scripts feature is enabled
        /// </summary>
        private static bool IsScriptsFeatureEnabled()
        {
            try
            {
                string enabledSetting = ConfigurationManager.AppSettings["EnableScripts"];
                return string.IsNullOrEmpty(enabledSetting) || 
                       enabledSetting.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error checking if scripts feature is enabled: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all scripts configured for a specific session event
        /// </summary>
        private static List<ScriptConfig> GetScriptsForEvent(SessionEvent sessionEvent)
        {
            List<ScriptConfig> scripts = new List<ScriptConfig>();

            try
            {
                // Check up to 20 script configurations
                for (int i = 1; i <= 20; i++)
                {
                    string scriptPath = ConfigurationManager.AppSettings[$"Script{i}Path"];
                    string scriptEvents = ConfigurationManager.AppSettings[$"Script{i}Events"];

                    if (string.IsNullOrEmpty(scriptPath) || string.IsNullOrEmpty(scriptEvents))
                    {
                        continue;
                    }

                    // Check if this script should run for the current event
                    if (ShouldRunForEvent(scriptEvents, sessionEvent))
                    {
                        ScriptConfig config = new ScriptConfig
                        {
                            Path = scriptPath.Trim(),
                            TimeoutSeconds = GetScriptTimeout(i),
                            PassSessionInfo = GetScriptPassSessionInfo(i)
                        };

                        scripts.Add(config);
                        Logger.LogInfo($"ScriptRunner: Added script '{config.Path}' for event '{sessionEvent}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error getting scripts for event '{sessionEvent}': {ex.Message}");
            }

            return scripts;
        }

        /// <summary>
        /// Check if script should run for the given event
        /// </summary>
        private static bool ShouldRunForEvent(string configuredEvents, SessionEvent sessionEvent)
        {
            try
            {
                string[] eventNames = configuredEvents.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string eventName in eventNames)
                {
                    string trimmedEvent = eventName.Trim();

                    if (trimmedEvent.Equals("AllSessions", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (trimmedEvent.Equals(sessionEvent.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error checking events '{configuredEvents}': {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get timeout setting for a script
        /// </summary>
        private static int GetScriptTimeout(int scriptNumber)
        {
            try
            {
                string timeoutSetting = ConfigurationManager.AppSettings[$"Script{scriptNumber}TimeoutSeconds"];
                
                if (int.TryParse(timeoutSetting, out int timeout) && timeout > 0)
                {
                    return timeout;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error getting timeout for script {scriptNumber}: {ex.Message}");
            }

            return 60; // Default timeout
        }

        /// <summary>
        /// Check if script should receive session info as parameters
        /// </summary>
        private static bool GetScriptPassSessionInfo(int scriptNumber)
        {
            try
            {
                string passSetting = ConfigurationManager.AppSettings[$"Script{scriptNumber}PassSessionInfo"];
                return passSetting?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScriptRunner: Error getting PassSessionInfo for script {scriptNumber}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Configuration for a single script
        /// </summary>
        private class ScriptConfig
        {
            public string Path { get; set; }
            public int TimeoutSeconds { get; set; }
            public bool PassSessionInfo { get; set; }
        }
    }
}