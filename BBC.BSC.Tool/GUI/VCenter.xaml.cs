using Meziantou.Framework.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
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
            InitializeComponent();
        }

        private const string appName = "BBC.BSC.Tool.vCenter";


        private void VCenter_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.cachedVcenter = Tool.VCenter.VCenter.CacheResults();

        }

        private void Install_Vmrc_Click(object sender, RoutedEventArgs e)
        {

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
                Process.Start(startInfo);

            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    // cancelled by user - ignore
                }
                else
                {
                    throw ex;
                }
            }
         
        }
    }
}
