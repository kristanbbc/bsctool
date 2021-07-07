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
        private Preparer _preparer;

        public Preparer Preparer => _preparer;

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

                _preparer = new Preparer();


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
                    if (Preparer.PrepareTool(Properties.Resources.vncx64, vncExeToRun))
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



    }
}
