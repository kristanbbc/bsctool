using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NLog;

namespace BBC.BSC.Tool.GUI
{
    /// <summary>
    /// Interaction logic for DiraLauncher.xaml - discovers locally installed DIRA tools and
    /// provides one-click buttons to launch them, similar to the BNCS VNC links tab.
    /// </summary>
    public partial class DiraLauncher : UserControl
    {
        private readonly Logger _logger = new Logging().InitLogger();

        /// <summary>
        /// Known DIRA install root directories, checked in order. Different machines/installers
        /// have historically used different roots (32-bit vs 64-bit Program Files, and the VCS
        /// vendor sub-folder), so we check all of them.
        /// </summary>
        private static readonly string[] DiraRoots =
        {
            @"C:\Program Files\VCS\dira",
            @"C:\Program Files\dira",
            @"C:\Program Files (x86)\dira"
        };

        /// <summary>
        /// Well-known DIRA tools, relative to a DIRA root directory, along with a friendly name
        /// to display on the launcher button.
        /// </summary>
        private static readonly (string Name, string RelativePath)[] KnownTools =
        {
            ("LogView", @"diraBasics\LogView.exe"),
            ("Log Dump", @"diraBasics\logdump.exe"),
            ("Highlander", @"diraHighlander\highlander.exe"),
            ("Scheduler", @"Scheduler\Scheduler.exe"),
            ("Broadcast Report", @"BcpNG\BroadcastReport.exe"),
            ("ATS Interface", @"Ats\ATS-Interface.exe"),
            ("ATS Player", @"Ats\ATS_PLAYER.exe"),
            ("ATS Recorder", @"Ats\ATS_RECORDER.exe"),
            ("Orion", @"Ats\Orion.exe"),
            ("Startrack", @"Ats\Startrack.exe"),
            ("XMix", @"Ats\XMix.exe")
        };

        public DiraLauncher()
        {
            InitializeComponent();
            BuildToolList();
        }

        private void BuildToolList()
        {
            string diraRoot = DiraRoots.FirstOrDefault(Directory.Exists);

            if (diraRoot == null)
            {
                _logger.Info("No DIRA installation found on this machine (checked: {0}).", string.Join(", ", DiraRoots));
                TextBlockEmpty.Visibility = Visibility.Visible;
                return;
            }

            _logger.Info("Found DIRA installation at '{0}'.", diraRoot);

            bool anyToolFound = false;

            foreach (var tool in KnownTools)
            {
                string fullPath = Path.Combine(diraRoot, tool.RelativePath);
                if (!File.Exists(fullPath))
                {
                    _logger.ConditionalTrace("DIRA tool '{0}' not found at '{1}', skipping.", tool.Name, fullPath);
                    continue;
                }

                anyToolFound = true;

                Button button = new Button
                {
                    Content = tool.Name,
                    Tag = fullPath,
                    Margin = new Thickness(0, 5, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(15, 5, 15, 5)
                };
                button.Click += LaunchButton_Click;

                _ = DiraToolsPanel.Children.Add(button);
            }

            TextBlockEmpty.Visibility = anyToolFound ? Visibility.Collapsed : Visibility.Visible;
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            string path = ((Button)sender).Tag as string;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _logger.Warn("DIRA tool path '{0}' no longer exists, cannot launch.", path);
                _ = MessageBox.Show($"Could not find '{path}'.", "DIRA Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _logger.Info("Launching DIRA tool: {0}", path);
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to launch DIRA tool '{0}'.", path);
                _ = MessageBox.Show($"Failed to launch '{path}':\n{ex.Message}", "DIRA Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
