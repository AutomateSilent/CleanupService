using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace CleanupService
{
    // This is a small test/debug class to check why the service might not be starting
    public class ServiceChecker
    {
        public static void CheckServiceStatus()
        {
            try
            {
                // Create log directory if it doesn't exist
                string logPath = @"C:\Logs\ProfileCleanUp";
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }
                
                // Create a diagnostic file
                string diagnosticFile = Path.Combine(logPath, "service_diagnostics.txt");
                using (StreamWriter writer = new StreamWriter(diagnosticFile, true))
                {
                    writer.WriteLine($"=== Service Diagnostic Check: {DateTime.Now} ===");
                    
                    // Check if service exists
                    ServiceController[] services = ServiceController.GetServices();
                    ServiceController ourService = null;
                    
                    foreach (ServiceController service in services)
                    {
                        if (service.ServiceName == "KioskProfileCleanupService")
                        {
                            ourService = service;
                            break;
                        }
                    }
                    
                    if (ourService == null)
                    {
                        writer.WriteLine("ERROR: Service not found in the system!");
                    }
                    else
                    {
                        writer.WriteLine($"Service found. Current status: {ourService.Status}");
                        writer.WriteLine($"Start type: {GetServiceStartType("KioskProfileCleanupService")}");
                        
                        // Check event logs for any service errors
                        writer.WriteLine("\nRecent service errors from Event Log:");
                        
                        try
                        {
                            EventLog eventLog = new EventLog("Application");
                            
                            // Look for events from our service or from the Service Control Manager
                            foreach (EventLogEntry entry in eventLog.Entries)
                            {
                                if ((entry.Source == "KioskProfileCleanupService" || 
                                     entry.Source == "Service Control Manager") && 
                                    entry.EntryType == EventLogEntryType.Error &&
                                    entry.TimeGenerated > DateTime.Now.AddDays(-1))
                                {
                                    writer.WriteLine($"Time: {entry.TimeGenerated}");
                                    writer.WriteLine($"Source: {entry.Source}");
                                    writer.WriteLine($"Message: {entry.Message}");
                                    writer.WriteLine(new string('-', 50));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"Error accessing event logs: {ex.Message}");
                        }
                        
                        // Try to start the service if it's not running
                        if (ourService.Status != ServiceControllerStatus.Running)
                        {
                            writer.WriteLine("\nAttempting to start the service...");
                            try
                            {
                                ourService.Start();
                                ourService.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                                writer.WriteLine($"After start attempt, service status: {ourService.Status}");
                            }
                            catch (Exception ex)
                            {
                                writer.WriteLine($"Failed to start service: {ex.Message}");
                            }
                        }
                    }
                    
                    writer.WriteLine($"Diagnostic completed at {DateTime.Now}");
                }
                
                Console.WriteLine($"Service diagnostic completed. Check {diagnosticFile} for details.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during diagnostic: {ex.Message}");
            }
        }
        
        private static string GetServiceStartType(string serviceName)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = $"qc {serviceName}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                // Parse the output to find START_TYPE
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.Trim().StartsWith("START_TYPE"))
                    {
                        return line.Trim();
                    }
                }
                
                return "Could not determine start type";
            }
            catch (Exception ex)
            {
                return $"Error getting start type: {ex.Message}";
            }
        }
    }
}
