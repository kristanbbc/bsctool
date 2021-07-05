using System;
﻿using NLog;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BBC.BSC.Tool.GUI
{
    /// <summary>
    /// Interaction logic for VCenter.xaml
    /// </summary>
    public partial class VCenter : UserControl
    {
        public VCenter()
        {
            Logging logging = new Logging();
            logger = logging.initLogger();

            logger.Info("initialising vCenter GUI");
            InitializeComponent();
        }

        private const string appName = "BBC.BSC.Tool.vCenter";
        private Logger logger;

        private async void VCenter_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;
            logger.Info("Starting routine to cache vCenter VMs");
            await Task.Run(() => MainWindow.cachedVcenter = Tool.VCenter.VCenter.CacheResults());

            logger.Info("Finished routine to cache vCenter VMs");
            ((Button)sender).IsEnabled = true;
        }

        private async void Install_Vmrc_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;

            logger.Info("Starting routine to install VMRC");

            try
            {

                var startInfo = new ProcessStartInfo();


                string dir = Path.Combine(Environment.CurrentDirectory, "tools");
                startInfo.FileName = @"\\national\bbcere\BSC\tools\VMRC\VMware-VMRC-12.0.0-17287072.exe";
                //startInfo.FileName = Path.Combine(dir, "VMware-VMRC-12.0.0-17287072.exe");
                //System.IO.File.Copy(Path.Combine(dir, "VMware-VMRC-12.0.0-17287072.exe"), Path.Combine(@"d:\", "vmrc.exe"), true);
                startInfo.WorkingDirectory = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine);
                //startInfo.FileName = Path.Combine(@"d:\", "vmrc.exe");
                startInfo.UseShellExecute = true;
                startInfo.Verb = "RunAs";
               await Task.Run(() => Process.Start(startInfo));

                logger.Info("VMRC installer closed cleanly");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                
                if (ex.NativeErrorCode == 1223)
                {
                    // cancelled by user - ignore
                    logger.Warn(ex);
                }
                else
                {
                    logger.Error(ex);
                    throw ex;
                }
            }
            finally
            {

                ((Button)sender).IsEnabled = true;
                logger.Info("Finished routine to install VMRC");
            }
         
        }
    }
}
