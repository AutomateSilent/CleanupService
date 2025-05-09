using System;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;

namespace CleanupService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            // If we have command line arguments, process them
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "-install":
                    case "/install":
                        InstallService();
                        return;

                    case "-uninstall":
                    case "/uninstall":
                        UninstallService();
                        return;

                    case "-test":
                    case "/test":
                        // Test mode - run cleanup operations directly without starting the service
                        Console.WriteLine("Running in test mode...");
                        // Initialize logger
                        Logger.Initialize();
                        Logger.LogInfo("Starting cleanup in test mode");
                        
                        // Run the cleanup directly
                        ProfileCleaner.RunCleanup("Manual Test");
                        
                        Console.WriteLine("Test completed. Check logs at: C:\\Logs\\ProfileCleanUp\\");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        return;
                }
            }

            // No arguments or unrecognized arguments, run as a service
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new CleanupService()
            };
            ServiceBase.Run(ServicesToRun);
        }

        private static void InstallService()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                Console.WriteLine("Service installed successfully!");
                Console.WriteLine("The service will start automatically at next system reboot.");
                Console.WriteLine("To start it immediately, use the Services management console or run:");
                Console.WriteLine("net start \"Kiosk Profile Cleanup Service\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error installing service: " + ex.Message);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static void UninstallService()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                Console.WriteLine("Service uninstalled successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error uninstalling service: " + ex.Message);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
