using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;

namespace CleanupService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            InitializeComponent();

            // Initialize the service process installer
            serviceProcessInstaller = new ServiceProcessInstaller();
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem; // Run as SYSTEM
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            // Initialize the service installer
            serviceInstaller = new ServiceInstaller();
            serviceInstaller.ServiceName = "KioskProfileCleanupService";
            serviceInstaller.DisplayName = "Kiosk Profile Cleanup Service";
            serviceInstaller.Description = "Performs profile cleanup on system startup, user logon, and user logoff events for kiosk environments.";
            serviceInstaller.StartType = ServiceStartMode.Automatic; // Start automatically
            serviceInstaller.DelayedAutoStart = false; // Use delayed auto-start

            // Add installers to collection
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
