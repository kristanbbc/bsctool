using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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

    private readonly Logger logger;

        public BncsVncLinks()
        {
            var logging = new Logging();
            logger = logging.initLogger();

            logger.Trace("Intilialisng BNCS Links View");
            InitializeComponent();


            if (Directory.Exists(BncsDir))
            {
                logger.Trace("Building BNCS View");
                Dispatcher.Invoke(BuildWs600View);
            }
            else
            {
                logger.Warn("BNCS path {0} not accessible, not building treeview", BncsDir);
            }
        }
        private const string BncsDir = @"\\national\bbcere\BSC\VNC\BNCS";

        private void BuildWs600View()
        {
            foreach (var item in Directory.GetDirectories(BncsDir))
            {
                logger.ConditionalTrace("Adding directory {0} to BNCS tree", item);
                var treeViewItem = new TreeViewItem
                {
                    Header = Path.GetFileNameWithoutExtension(item),
                    Tag = item
                };
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                stack.Children.Add(new PackIcon { Kind = PackIconKind.Folder });
                stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                treeViewItem.Header = stack;
                treeViewItem.Items.Add(null);
                treeViewItem.Expanded += TreeViewBNCS_Expanded;

                TreeViewBncs.Items.Add(treeViewItem);

            }
            foreach (var item in Directory.GetFiles(BncsDir))
            {
                logger.ConditionalTrace("Adding file {0} to BNCS tree", item);
                var treeViewItem = new TreeViewItem
                {
                    Header = Path.GetFileNameWithoutExtension(item),
                    Tag = item
                };
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                var ext = Path.GetExtension(item).Substring(1).ToLower();
                stack.Children.Add(new PackIcon() { Kind = Modules.BNCS.GetPackIconKind(ext) });
                stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                treeViewItem.Header = stack;
                treeViewItem.MouseDoubleClick += TreeViewBNCS_DoubleClicked;
                TreeViewBncs.Items.Add(treeViewItem);
            }
        }

        private void TreeViewBNCS_DoubleClicked(object sender, MouseButtonEventArgs e)
        {
            var tvSender = (TreeViewItem)sender;

            var fileInfo = new FileInfo(tvSender.Tag.ToString());
            var startInfo = new ProcessStartInfo();

            switch (fileInfo.Extension.ToLower().Substring(1))
            {
                case "vnc":
                    var vncExeToRun = Path.Combine(Path.GetTempPath(), "vncx64.exe");
                    var __ = PrepareTool(Properties.Resources.vncx64, vncExeToRun);
                    if (__)
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

            if (startInfo.FileName.Length <= 0) return;
             logger.Info("Starting: {0} with argumets {1}", startInfo.FileName, startInfo.Arguments);
            Process.Start(startInfo);

        }


        private void TreeViewBNCS_Expanded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(delegate
            {
                var tvSender = (TreeViewItem)sender;
                if (tvSender.Items.Count != 1 || tvSender.Items[0] != null) return;

                tvSender.Items.Clear();

                foreach (string item in Directory.GetDirectories(tvSender.Tag.ToString()))
                {
                    logger.ConditionalTrace("Adding directory {0} to BNCS tree", item);
                    var treeViewItem = new TreeViewItem
                    {
                        Header = Path.GetFileNameWithoutExtension(item),
                        Tag = item
                    };

                    var stack = new StackPanel { Orientation = Orientation.Horizontal };
                    stack.Children.Add(new PackIcon { Kind = PackIconKind.Folder });
                    stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                    treeViewItem.Header = stack;
                    treeViewItem.Items.Add(null);
                    treeViewItem.Expanded += TreeViewBNCS_Expanded;

                    tvSender.Items.Add(treeViewItem);

                }
                foreach (string item in Directory.GetFiles(tvSender.Tag.ToString()))
                {
                    logger.ConditionalTrace("Adding file {0} to BNCS tree", item);
                    var treeViewItem = new TreeViewItem
                    {
                        Header = Path.GetFileNameWithoutExtension(item),
                        Tag = item
                    };
                    var stack = new StackPanel { Orientation = Orientation.Horizontal };
                    var ext = Path.GetExtension(item).Substring(1).ToLower();
                    stack.Children.Add(new PackIcon { Kind = Modules.BNCS.GetPackIconKind(ext) });
                    stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                    treeViewItem.Header = stack;
                    treeViewItem.MouseDoubleClick += TreeViewBNCS_DoubleClicked;

                    tvSender.Items.Add(treeViewItem);
                }
            });
        }






        public bool PrepareTool(byte[] resource, string outputPath)
        {
            logger.Trace("Preparing tool to path {0}", outputPath);
            if (File.Exists(outputPath))
            {
               logger.Trace("Tool path already exists.");
                //check md5
                byte[] existingMd5;
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(outputPath))
                    {
                        existingMd5 = md5.ComputeHash(stream);
                    }
                }

                //md5 of embedded resource
                byte[] resourceMd5;
                using (var md5 = MD5.Create())
                {
                    md5.TransformFinalBlock(resource, 0, resource.Length);
                    resourceMd5 = md5.Hash;
                }

                if (Encoding.Default.GetString(existingMd5) == Encoding.Default.GetString(resourceMd5))
                {
                    logger.Trace("Tool path exists and MD5 matches, returning true");
                    return true;
                }

                logger.Warn("Tool path exists, but MD5 doesn't match, remove file and retest");
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
                    logger.Debug("Tool written to {0}, returning true", outputPath);
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error("Problem writing out tool. {0}", ex.Message);
                }
            }
            return false;
        }


    }
}
