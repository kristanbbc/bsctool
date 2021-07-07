using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using NLog;
using Path = System.IO.Path;

namespace BBC.BSC.Tool.GUI
{
    /// <summary>
    /// Interaction logic for BncsVncLinks.xaml
    /// </summary>
    public partial class BncsVncLinks : UserControl
    {
        private const string BncsDir = @"\\national\bbcere\BSC\VNC\BNCS";
        private readonly Logger _logger = new Logging().InitLogger();

        /// <summary>
        /// 
        /// </summary>
        public BncsVncLinks()
        {

           //Logger logger = new Logging().initLogger();
            _logger.Trace("Intilialisng BNCS Links View");
            InitializeComponent();

            if (Directory.Exists(BncsDir))
            {
                _logger.Trace("Building BNCS View");
                Dispatcher.Invoke(BuildWs600View);
            }
            else
            {
                _logger.Warn("BNCS path {0} not accessible, not building treeview", BncsDir);
            }
        }

        /// <summary>
        /// Builds the BNCS view
        /// </summary>
        private void BuildWs600View()
        {

            foreach (var item in Directory.GetDirectories(BncsDir))
            {
                _logger.ConditionalTrace("Adding directory {0} to BNCS tree", item);
                TreeViewItem treeViewItem = new TreeViewItem
                {
                    Header = Path.GetFileNameWithoutExtension(item),
                    Tag = item
                };
                StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
                _ = stack.Children.Add(new PackIcon { Kind = PackIconKind.Folder });
                _ = stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                treeViewItem.Header = stack;
                _ = treeViewItem.Items.Add(null);
                treeViewItem.Expanded += TreeViewBNCS_Expanded;

                _ = TreeViewBncs.Items.Add(treeViewItem);

            }
            foreach (var item in Directory.GetFiles(BncsDir))
            {
                _logger.ConditionalTrace("Adding file {0} to BNCS tree", item);
                StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
                string ext = Path.GetExtension(item).Substring(1).ToLower();
                _ = stack.Children.Add(new PackIcon() { Kind = Modules.Bncs.GetPackIconKind(ext) });
                _ = stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                TreeViewItem treeViewItem = new TreeViewItem
                {
                    Header = Path.GetFileNameWithoutExtension(item),
                    Tag = item
                };
                treeViewItem.Header = stack;
                treeViewItem.MouseDoubleClick += TreeViewBNCS_DoubleClicked;
                _ = TreeViewBncs.Items.Add(treeViewItem);
            }
        }

        private void TreeViewBNCS_DoubleClicked(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem tvSender = (TreeViewItem)sender;
            FileInfo fileInfo = new FileInfo(tvSender.Tag.ToString());
            ProcessStartInfo startInfo = new ProcessStartInfo();

            switch (fileInfo.Extension.ToLower().Substring(1))
            {
                case "vnc":
                    string vncExeToRun = Path.Combine(Path.GetTempPath(), "vncx64.exe");
                    if (PrepareTool(Properties.Resources.vncx64, vncExeToRun))
                    {
                        startInfo.Arguments = $"\"{tvSender.Tag}\"";
                        startInfo.FileName = vncExeToRun;
                    }

                    break;
                case "url":
                    startInfo.FileName = $"\"{tvSender.Tag}\"";

                    break;
                default:
                    startInfo.FileName = tvSender.Tag.ToString(); // $"\" { tvSender.Tag} \"";

                    break;
            }

            if (startInfo.FileName.Length <= 0)
            {
                return;
            }

            _logger.Info("Starting: {0} with argumets {1}", startInfo.FileName, startInfo.Arguments);
            _ = Process.Start(startInfo);

        }


        private void TreeViewBNCS_Expanded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(delegate
            {
                TreeViewItem tvSender = (TreeViewItem)sender;
                if (tvSender.Items.Count == 1 && tvSender.Items[0] == null)
                {
                    tvSender.Items.Clear();

                    foreach (string item in Directory.GetDirectories(tvSender.Tag.ToString()))
                    {
                        _logger.ConditionalTrace("Adding directory {0} to BNCS tree", item);
                        TreeViewItem treeViewItem = new TreeViewItem
                        {
                            Header = Path.GetFileNameWithoutExtension(item),
                            Tag = item
                        };

                        StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
                        _ = stack.Children.Add(new PackIcon { Kind = PackIconKind.Folder });
                        _ = stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                        treeViewItem.Header = stack;
                        _ = treeViewItem.Items.Add(null);
                        treeViewItem.Expanded += TreeViewBNCS_Expanded;

                        _ = tvSender.Items.Add(treeViewItem);

                    }
                    foreach (string item in Directory.GetFiles(tvSender.Tag.ToString()))
                    {
                        _logger.ConditionalTrace("Adding file {0} to BNCS tree", item);
                        StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
                        string ext = Path.GetExtension(item).Substring(1).ToLower();
                        _ = stack.Children.Add(new PackIcon { Kind = Modules.Bncs.GetPackIconKind(ext) });
                        _ = stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                        TreeViewItem treeViewItem = new TreeViewItem
                        {
                            Header = Path.GetFileNameWithoutExtension(item),
                            Tag = item
                        };
                        treeViewItem.Header = stack;
                        treeViewItem.MouseDoubleClick += TreeViewBNCS_DoubleClicked;

                        _ = tvSender.Items.Add(treeViewItem);
                    }
                }
            });
        }


        /// <summary>
        /// Prepares an external tool for use from an embedded resource.
        /// </summary>
        /// <param name="resource">embedded resource</param>
        /// <param name="outputPath">where to place the tool on the system</param>
        /// <returns></returns>
        public bool PrepareTool(byte[] resource, string outputPath)
        {
            _logger.Trace("Preparing tool to path {0}", outputPath);
            if (File.Exists(outputPath))
            {
                _logger.Trace("Tool path already exists.");
                //check md5
                byte[] existingMd5;
                using (var md5 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(outputPath))
                    {
                        existingMd5 = md5.ComputeHash(stream);
                    }
                }

                //md5 of embedded resource
                byte[] resourceMd5;
                using (var md5 = SHA256.Create())
                {
                    md5.TransformFinalBlock(resource, 0, resource.Length);
                    resourceMd5 = md5.Hash;
                }

                if (Encoding.Default.GetString(existingMd5) == Encoding.Default.GetString(resourceMd5))
                {
                    _logger.Trace("Tool path exists and SHA256 matches, returning true");
                    return true;
                }

                _logger.Warn("Tool path exists, but SHA256 doesn't match, remove file and retest");
                File.Delete(outputPath);

                PrepareTool(resource, outputPath);
                // return false;
            }
            else
            {
                try
                {
                    // logger.Trace("Tool doesn't exist, writing out new file");
                    using (FileStream exeFile = new FileStream(outputPath, FileMode.Create))
                    {
                        exeFile.Write(resource, 0, resource.Length);
                    }
                    _logger.Debug("Tool written to {0}, returning true", outputPath);
                    return true;
                }
                catch (IOException ex)
                {
                    _logger.Error("Problem writing out tool. {0}", ex.Message);
                    _ = MessageBox.Show($"Unable to write tool to {outputPath}", "Error in preparing tool", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            return false;
        }


    }
}
