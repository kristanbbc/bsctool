using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
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
        private readonly Dictionary<string, int> _sites = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] DiraRoots =
        {
            @"C:\Program Files\VCS\dira",
            @"C:\Program Files\dira",
            @"C:\Program Files (x86)\dira"
        };

        private static readonly string[] HighlanderPaths =
        {
            @"C:\Program Files\VCS\dira\diraHighlander\highlander.exe",
            @"C:\Program Files\dira\diraHighlander\highlander.exe",
            @"C:\Program Files (x86)\dira\diraHighlander\highlander.exe"
        };

        private static readonly (string Name, string RelativePath, string Arguments)[] KnownTools =
        {
            ("LogView", @"diraBasics\LogView.exe", ""),
            ("Log Dump", @"diraBasics\logdump.exe", ""),
            ("Highlander", @"diraHighlander\highlander.exe", ""),
            ("Scheduler", @"Scheduler\Scheduler.exe", ""),
            ("Broadcast Report", @"BcpNG\BroadcastReport.exe", ""),
            ("ATS Interface", @"Ats\ATS-Interface.exe", ""),
            ("ATS Player", @"Ats\ATS_PLAYER.exe", ""),
            ("ATS Recorder", @"Ats\ATS_RECORDER.exe", ""),
            ("Orion", @"Ats\Orion.exe", ""),
            ("Startrack", @"Ats\Startrack.exe", ""),
            ("XMix", @"Ats\XMix.exe", ""),
            ("DiraTools", @"ToolsNG\DiraTools.exe", "/S+")
        };

        public DiraLauncher()
        {
            InitializeComponent();
            BuildToolList();
            BuildSitesList();
        }

        private void BuildToolList()
        {
            bool anyToolFound = false;

            foreach (var tool in KnownTools)
            {
                string fullPath = DiraRoots
                    .Select(root => Path.Combine(root, tool.RelativePath))
                    .FirstOrDefault(File.Exists);

                if (string.IsNullOrEmpty(fullPath))
                {
                    _logger.ConditionalTrace("DIRA tool '{0}' not found in known install roots.", tool.Name);
                    continue;
                }

                anyToolFound = true;

                Button button = new Button
                {
                    Content = tool.Name,
                    Tag = (fullPath, tool.Arguments),
                    Margin = new Thickness(0, 5, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(15, 5, 15, 5)
                };
                button.Click += LaunchButton_Click;

                _ = DiraToolsPanel.Children.Add(button);
            }

            TextBlockToolsEmpty.Visibility = anyToolFound ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BuildSitesList()
        {
            _sites.Clear();
            ListBoxSites.Items.Clear();

            string registryPath = Environment.Is64BitOperatingSystem
                ? @"SOFTWARE\Wow6432Node\VCS\dira\fw_netcom"
                : @"SOFTWARE\VCS\dira\fw_netcom";

            using (RegistryKey fwNetcomKey = Registry.LocalMachine.OpenSubKey(registryPath, false))
            {
                if (fwNetcomKey == null)
                {
                    _logger.Info("DIRA site registry path not found: HKLM\\{0}", registryPath);
                    TextBlockSitesEmpty.Visibility = Visibility.Visible;
                    return;
                }

                foreach (string subKeyName in fwNetcomKey.GetSubKeyNames())
                {
                    string[] parts = subKeyName.Split('_');
                    if (parts.Length < 2 || !int.TryParse(parts[0], out int ntcIndex))
                    {
                        _logger.ConditionalTrace("Skipping unrecognized fw_netcom key format: {0}", subKeyName);
                        continue;
                    }

                    string siteName = parts[1];
                    _sites[siteName] = ntcIndex;
                }
            }

            foreach (string siteName in _sites.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                _ = ListBoxSites.Items.Add(siteName);
            }

            TextBlockSitesEmpty.Visibility = ListBoxSites.Items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).Tag is ValueTuple<string, string> launchInfo))
            {
                return;
            }

            string path = launchInfo.Item1;
            string arguments = launchInfo.Item2;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _logger.Warn("DIRA tool path '{0}' no longer exists, cannot launch.", path);
                _ = MessageBox.Show($"Could not find '{path}'.", "DIRA Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _logger.Info("Launching DIRA tool: {0} {1}", path, arguments);
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to launch DIRA tool '{0}'.", path);
                _ = MessageBox.Show($"Failed to launch '{path}':\n{ex.Message}", "DIRA Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListBoxSites_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonLaunchHighlander.IsEnabled = ListBoxSites.SelectedItem != null;
        }

        private void ButtonLaunchHighlander_Click(object sender, RoutedEventArgs e)
        {
            if (!(ListBoxSites.SelectedItem is string siteName) || !_sites.TryGetValue(siteName, out int ntcIndex))
            {
                _ = MessageBox.Show("Please select a site.", "DIRA Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string highlanderPath = HighlanderPaths.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(highlanderPath))
            {
                _logger.Warn("Highlander executable not found in known paths.");
                _ = MessageBox.Show("Could not find Highlander executable.", "DIRA Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string args = $"/ntcindex {ntcIndex}";
                _logger.Info("Launching Highlander: {0} {1}", highlanderPath, args);
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = highlanderPath,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(highlanderPath) ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to launch Highlander for site '{0}'.", siteName);
                _ = MessageBox.Show($"Failed to launch Highlander:\n{ex.Message}", "DIRA Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
