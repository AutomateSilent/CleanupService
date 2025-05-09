using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Timers;
using System.Threading;

namespace CleanupService
{
    public partial class CleanupService : ServiceBase
    {
        private System.Timers.Timer _startupTimer;
        private System.Timers.Timer _heartbeatTimer;

        public CleanupService()
        {
            InitializeComponent();

            // Configure service properties
            this.ServiceName = "KioskProfileCleanupService";
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanShutdown = true;
            this.CanStop = true;
            this.AutoLog = false;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // Initialize the logger
                Logger.Initialize();
                Logger.LogInfo("Service starting...");

                // We'll use a short delay before running startup cleanup
                // to ensure the system is fully initialized
                _startupTimer = new System.Timers.Timer
                {
                    Interval = 60000, // 60 seconds
                    AutoReset = false
                };
                _startupTimer.Elapsed += StartupTimer_Elapsed;
                _startupTimer.Start();

                // Create a heartbeat timer to ensure service stays alive
                _heartbeatTimer = new System.Timers.Timer
                {
                    Interval = 3600000, // 1 hour
                    AutoReset = true
                };
                _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
                _heartbeatTimer.Start();

                Logger.LogInfo("Service started successfully. Startup cleanup will run in 60 seconds.");
            }
            catch (Exception ex)
            {
                // Log any errors during startup
                EventLog.WriteEntry("KioskProfileCleanupService", 
                    $"Error starting service: {ex.Message}", 
                    EventLogEntryType.Error);

                // Try to log to our custom log if possible
                try { Logger.LogError($"Error starting service: {ex.Message}"); } catch { }
            }
        }

        private void HeartbeatTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // This heartbeat just ensures the service is still running
                // and logs a periodic message to confirm it's alive
                Logger.LogInfo("Service heartbeat - still running");
            }
            catch (Exception ex)
            {
                // Log but don't let any exception escape
                try { Logger.LogError($"Error in heartbeat: {ex.Message}"); } catch { }
            }
        }

        private void StartupTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.LogInfo("Running startup cleanup...");
                
                // Run the cleanup in a separate thread to avoid any timing issues
                ThreadPool.QueueUserWorkItem(state => {
                    try
                    {
                        ProfileCleaner.RunCleanup("System Startup");
                        Logger.LogInfo("Startup cleanup completed");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error in startup cleanup thread: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Log but don't let exception terminate the timer thread
                Logger.LogError($"Error in startup cleanup handler: {ex.Message}");
            }
        }

        protected override void OnStop()
        {
            try
            {
                Logger.LogInfo("Service stopping");

                // Cleanup timers
                if (_startupTimer != null)
                {
                    _startupTimer.Stop();
                    _startupTimer.Dispose();
                    _startupTimer = null;
                }

                if (_heartbeatTimer != null)
                {
                    _heartbeatTimer.Stop();
                    _heartbeatTimer.Dispose();
                    _heartbeatTimer = null;
                }

                Logger.LogInfo("Service stopped");
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("KioskProfileCleanupService", 
                    $"Error stopping service: {ex.Message}", 
                    EventLogEntryType.Error);
            }
        }

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            try
            {
                switch (changeDescription.Reason)
                {
                    case SessionChangeReason.SessionLogon:
                        Logger.LogInfo($"Session logon detected (Session ID: {changeDescription.SessionId})");

                        // Run on a background thread to avoid blocking the service
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            try
                            {
                                ProfileCleaner.RunCleanup("User Logon");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error in logon cleanup thread: {ex.Message}");
                            }
                        });
                        break;

                    case SessionChangeReason.SessionLogoff:
                        Logger.LogInfo($"Session logoff detected (Session ID: {changeDescription.SessionId})");

                        // Run on a background thread to avoid blocking the service
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            try
                            {
                                ProfileCleaner.RunCleanup("User Logoff");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error in logoff cleanup thread: {ex.Message}");
                            }
                        });
                        break;

                    case SessionChangeReason.SessionLock:
                        Logger.LogInfo($"Session lock detected (Session ID: {changeDescription.SessionId})");

                        // Run on a background thread to avoid blocking the service
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            try
                            {
                                ProfileCleaner.RunCleanup("Session Lock");
                                ProfileCleaner.CloseConfiguredProcesses(); // Close specified processes on lock
                                ProfileCleaner.RestartExplorer(); // Restart Explorer 
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error in session lock cleanup thread: {ex.Message}");
                            }
                        });
                        break;

                    case SessionChangeReason.SessionUnlock:
                        Logger.LogInfo($"Session unlock detected (Session ID: {changeDescription.SessionId})");

                        // Run on a background thread to avoid blocking the service
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            try
                            {
                                ProfileCleaner.RunCleanup("Session Unlock");
                                ProfileCleaner.CloseConfiguredProcesses();
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error in session unlock cleanup thread: {ex.Message}");
                            }
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                // Catch all exceptions to ensure service doesn't crash
                Logger.LogError($"Error in session change handler: {ex.Message}");
            }
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            try
            {
                // Handle power events if needed
                Logger.LogInfo($"Power event detected: {powerStatus}");
                
                // You could add specific handling for sleep/resume events
                // For example, run cleanup when system resumes from sleep
                if (powerStatus == PowerBroadcastStatus.ResumeSuspend)
                {
                    Logger.LogInfo("System resuming from sleep, running cleanup");
                    
                    // Run on a background thread
                    ThreadPool.QueueUserWorkItem(state => {
                        try 
                        {
                            ProfileCleaner.RunCleanup("Resume From Sleep");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Error in resume cleanup thread: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in power event handler: {ex.Message}");
            }
            
            return base.OnPowerEvent(powerStatus);
        }

        protected override void OnShutdown()
        {
            try
            {
                Logger.LogInfo("System shutdown detected, performing final cleanup");
                
                // Run a quick cleanup on shutdown if needed - in this case we can run synchronously
                // since the system is shutting down anyway
                ProfileCleaner.RunCleanup("System Shutdown");
                
                Logger.LogInfo("Shutdown cleanup completed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in shutdown handler: {ex.Message}");
            }
            
            base.OnShutdown();
        }
    }
}
