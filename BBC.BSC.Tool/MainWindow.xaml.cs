using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BBC.BSC.Tool.Properties;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.ElasticSearch;
using Timer = System.Timers.Timer;

namespace BBC.BSC.Tool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        //TODO make more of these configurable?
        private const string CatPath = @"http://cat.er.bbc.co.uk/catquery.php?json&query=";

        private readonly List<BackgroundWorker> workers = new List<BackgroundWorker>();
        // ReSharper disable once CollectionNeverQueried.Local
        private readonly List<BackgroundWorker> connectionWorkers = new List<BackgroundWorker>();
        private DateTime lastConnectionResult = DateTime.Now;
        private DateTime lastResultTimestamp;
        private const string LdapPath = @"LDAP://ldap.national.core.bbc.co.uk";
        private readonly Timer searchTimer = new Timer(400);
        private readonly Timer hostTimer = new Timer(400);
        private string hostText;
        private const string BncsDir = @"\\national\bbcere\BSC\VNC\BNCS";
        private readonly Logger logger;

        private const string PhoneboxIniPath = @"C:\ProgramData\Broadcast Bionics\PhoneBOX4\client.ini";
        private const string PhoneboxExePath = @"C:\Program Files (x86)\Broadcast Bionics\PhoneBOX4\Client\PhoneBOX.Client.exe";


        public MainWindow()
        {
            InitializeComponent();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;

            Title = "BSC Tool - Version " + version;
            var config = new LoggingConfiguration();
            var consoleTarget = new ColoredConsoleTarget
            {
                // ReSharper disable once StringLiteralTypo
                Layout = "${time} ${pad:padding=3:inner=${threadid}} ${message} ${exception:format=tostring}"
            };
            var elasticSearchTarget = new ElasticSearchTarget
            {
                Index = "bsctool1",
                IncludeAllProperties = true
            };
            elasticSearchTarget.Fields.Add(new Field { Name = "user", Layout = "${windows-identity:userName=True:domain=False}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "host", Layout = "${machinename}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "thread", Layout = "${threadid}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "threadname", Layout = "${threadname}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "version", Layout = "${assembly-version}" });
#if DEBUG
            elasticSearchTarget.Fields.Add(new Field { Name = "build", Layout = "DEBUG" });

#else
            elasticSearchTarget.Fields.Add(new NLog.Targets.ElasticSearch.Field() { Name = "build", Layout = "RELEASE" });

#endif

            elasticSearchTarget.Layout = "${message} ${exception:format=tostring}";

            elasticSearchTarget.Uri = @"http://3gbbmdbels1000:9200";
            config.AddRule(LogLevel.Info, LogLevel.Fatal, elasticSearchTarget);

            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);

            logger = LogManager.GetCurrentClassLogger();
            LogManager.Configuration = config;

            logger.Info("BSC Tool {0} starting.", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            var watcher = new Timer
            {
                Interval = 1000
            };
            watcher.Elapsed += Do_Watcher;
            watcher.Enabled = true;
            ThreadPool.GetMinThreads(out var w, out var c);

            // Write the numbers of minimum threads
            logger.Debug("Minumium number of threads available {0}, {1}", w, c);

            ThreadPool.SetMinThreads(20, 10);
            hostTimer.Elapsed += Host_Timer_Elapsed;
            searchTimer.Elapsed += Search_Timer_Elapsed;
            DataContext = this;

            foreach (var item in Settings.Default.history.Split(';'))
            {
                if (item != "System.Windows.Controls.ItemCollection") LvHistory.Items.Add(item);
            }


            //If PhoneBox not installed don't enable tab
            if (!File.Exists(PhoneboxExePath))
            {
                PhoneBoxSwitcherTab.IsEnabled = false;
                foreach (var item in UiHelper.FindVisualChildren<Button>(PhoneBoxButtons))
                {
                    item.IsEnabled = false;
                }
            }
            if (Directory.Exists(BncsDir))
            {
                Dispatcher.Invoke(BuildWs600View);
            }
            else
            {
                TabItemBncsVnc.IsEnabled = false;
            }

            //Dispatcher.Invoke(delegate
            //{
            //    UpdateInfoGridAllocation();
            //});

            // Put Cursor in search box.
            SearchIn.Focus();
        }


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
                stack.Children.Add(new PackIcon() { Kind = GetPackIconKind(ext) });
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
                    stack.Children.Add(new PackIcon { Kind = GetPackIconKind(ext) });
                    stack.Children.Add(new Label { Content = Path.GetFileNameWithoutExtension(item) });
                    treeViewItem.Header = stack;
                    treeViewItem.MouseDoubleClick += TreeViewBNCS_DoubleClicked;

                    tvSender.Items.Add(treeViewItem);
                }
            });
        }

        private static PackIconKind GetPackIconKind(string ext)
        {
            PackIconKind packIconKind;
            switch (ext)
            {
                case "vnc":
                    packIconKind = PackIconKind.Computer;
                    break;
                case "url":
                    packIconKind = PackIconKind.Web;
                    break;
                case "lnk":
                    packIconKind = PackIconKind.FolderNetwork;
                    break;
                case "rdp":
                    packIconKind = PackIconKind.Server;
                    break;
                default:
                    packIconKind = PackIconKind.HelpBox;
                    break;
            }
            return packIconKind;
        }


        /// <summary>Updates the status box if running searches are still happening.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Do_Watcher(object sender, ElapsedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(delegate
                {
                    if (workers.Count > 0)
                    {
                        logger.Debug("There are {0} workers", workers.Count);
                        Status.Fill = new SolidColorBrush(Colors.Red);
                        // this.Title = "Busy";
                    }
                    else
                    {
                        Status.Fill = new SolidColorBrush(Colors.Green);
                        //this.Title = "Finished";
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Fatal(ex);
                //throw;
            }

        }

        private List<CatResult> catResults = new List<CatResult>();
        private class CatResult
        {
            [JsonProperty("host_name")]
            public string HostName;
            [JsonProperty("also_known_as")]
            public string AlsoKnownAs;
            [JsonProperty("ip")]
            public string Ip;
            [JsonProperty("os")]
            public string Os;

        }

        private void Do_Search(object sender, DoWorkEventArgs e)
        {
            Dispatcher.Invoke(delegate
            {
                Status.Fill = new SolidColorBrush(Colors.Red);
            });
            MyResults results = new MyResults();
            if (e.Argument.ToString().Length < 4)
            {
                e.Result = null;
            }
            else
            {
                logger.Info("{1} DoSearch: {0}", e.Argument.ToString(), Environment.CurrentManagedThreadId);
                try
                {
                    var catQuery =
                        $"{CatPath}SELECT host_name, also_known_as, CAST(inet_ntoa(ip) as CHAR(15)) as ip, CONCAT(os,  \" \",os_version) as os FROM " +
                        "network INNER JOIN asset ON network.asset_id = asset.asset_id " +
                        "left join asset_os on asset.asset_id = asset_os.asset_id left join os on asset_os.os_id = os.os_id left join os_version on asset_os.os_version_id = os_version.os_version_id " +
                        $"WHERE life_cycle_status_id = 4 AND (lower(host_name like '%{e.Argument.ToString().Replace("*", "%").ToLower()}%') OR IP = inet_aton('{e.Argument.ToString().Replace("*", "%").ToLower()}') OR lower(also_known_as) LIKE '%{e.Argument.ToString().Replace("*", "%").ToLower()}%')";

                    string jsonData;
                    logger.Info("Running query against CAT with\n{0}", catQuery);
                    using (var w = new WebClient())
                    {
                        w.UseDefaultCredentials = true;
                        jsonData = w.DownloadString(catQuery);
                    }

                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        catResults = JsonConvert.DeserializeObject<List<CatResult>>(jsonData);
                        logger.Info("Got {0} results from CAT", catResults?.Count);

                        if (catResults != null)
                            foreach (var item in catResults)
                            {
                                results.Results.Add(new MyResult
                                {
                                    Source = "CAT",
                                    Hostname = item.HostName.ToUpper(),
                                    Description = item.AlsoKnownAs,
                                    OperatingSystem = item.Os,
                                    Ip = item.Ip
                                });
                            }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn("Error running query against CAT:\n{0}", ex.Message);
                    Trace.TraceError(ex.Message);
                }

                // Start AD search
                try
                {
                    using (DirectoryEntry dEntry = new DirectoryEntry(LdapPath))
                    using (DirectorySearcher dSearcher = new DirectorySearcher(dEntry)
                    {
                        // (|(cn=*334810*)(displayname=*334810*)(cn=PC-*334810*)(cn=B1-D0*334810*)(cn=B1-L0*334810*)(cn=61-D0*334810*)(cn=61-L0*334810*)(cn=71-D0*334810*)(cn=71-L0*334810*)(cn=91-D0*334810*)(cn=91-L0*334810*)(cn=F1-D0*334810*)(cn=F1-L0*334810*)(cn=MC-*334810*)(sn=*334810*)(samAccountName=*334810*)(mail=*334810*)(proxyaddresses=smtp:*334810*)(ou=*334810*)(&(objectcategory=printqueue)(printername=*334810*)))
                        //Filter = string.Format("(&(objectClass=computer)(cn={0}*))", e.Argument.ToString()),
                        Filter = string.Format("(&(!userAccountControl:1.2.840.113556.1.4.803:=2)(objectClass=computer)(|(cn={0}*)(displayname={0}*)(cn=PC-{0}*)(cn=B1-D0{0}*)(cn=B1-L0{0}*)(cn=31-D0{0}*)(cn=31*-D0{0}*)(cn=61-D0{0}*)(cn=61-L0{0}*)(cn=71-D0{0}*)(cn=71-L0{0}*)(cn=91-D0{0}*)(cn=91-L0{0}*)(cn=F1-D0{0}*)(cn=F1-L0{0}*)(cn=MC-{0}*)(sn={0}*)(samAccountName={0}*)))", e.Argument),
                        //PageSize = 20,
                        //ServerTimeLimit = TimeSpan.FromSeconds(15),
                        //ServerPageTimeLimit = TimeSpan.FromSeconds(15),
                        //SizeLimit = 20,
                        ClientTimeout = TimeSpan.FromSeconds(15)
                    })
                    {
                        dSearcher.PropertiesToLoad.Clear();
                        dSearcher.PropertiesToLoad.Add("name");
                        dSearcher.PropertiesToLoad.Add("description");
                        dSearcher.PropertiesToLoad.Add("operatingsystem");
                        using (var sResults = dSearcher.FindAll())
                        {
                            logger.Info("Found {0} results in Active Directory", sResults.Count);
                            foreach (SearchResult item in sResults)
                            {
                                logger.ConditionalTrace("AD: found: {0}", item.Properties["name"][0].ToString().ToUpper());
                                if (results.Results.All(n => n.Hostname != item.Properties["name"][0].ToString().ToUpper()))
                                {
                                    results.AddResult(new MyResult
                                    {
                                        Hostname = item.Properties["name"][0].ToString().ToUpper(),
                                        Description = CleanResultProperty(item, "description"),
                                        Ip = "Load IP",
                                        OperatingSystem = CleanResultProperty(item, "operatingSystem"),
                                        Source = "AD"
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message);
                    logger.Warn("LDAP query error: {0}", ex.Message);
                }
            }
            results.Results.Sort((a, b) => string.Compare(a.Hostname, b.Hostname, StringComparison.Ordinal));
            results.Results = results.Results.Distinct().ToList();
            e.Result = results;
        }

        private static string CleanResultProperty(SearchResult item, string property)
        {
            return (item.Properties.Contains(property) ? item.Properties[property][0].ToString() : "");
        }

        private void Text_Changed(object sender, TextChangedEventArgs e)
        {
            logger.ConditionalTrace("search text changed: {0}", SearchIn.Text.Trim());
            searchText = SearchIn.Text;
            searchTimer.Stop();
            searchTimer.Start();
            TextBoxHost.Text = SearchIn.Text;
        }


        private MyResult selectedResult = new MyResult();

        private void DisplayResults(object sender, RunWorkerCompletedEventArgs e)
        {
            logger.Trace("Start displaying results, stopping and disposing background worker");
            workers.Remove((BackgroundWorker)sender);
            ((BackgroundWorker)sender).Dispose();
            var res = (MyResults)e.Result;
            logger.Trace($"There are {res.Results.Count} results");
            // If results returned select first in list.
            selectedResult = res.Results.Count > 0 ? ((MyResults)e.Result).Results[0] : null;
            try
            {
                // If results older than currently displayed (earlier queries take longer) then drop results
                if (res.Timestamp <= lastResultTimestamp) return;
                logger.Trace("Results newer so continue processing");
                Dispatcher.Invoke(delegate
                {
                    logger.Trace("Copying results to result collection.");
                    var results = new ObservableCollection<MyResult>();
                    foreach (var item in res.Results)
                    {
                        results.Add(item);
                    }
                    SearchResults.ItemsSource = null;
                    SearchResults.ItemsSource = results;

                    if (res.Results.Count != 1) return;
                    logger.Trace("Single result so make selected");
                    TextBoxHost.Text = res.Results[0].Hostname;
                    //searchResults.Items.Refresh();
                });
                lastResultTimestamp = res.Timestamp;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                logger.Error("Problem displaying results:\n{0}", ex.Message);
                App.SendReport(ex);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Settings.Default.Save();
            if (workers.Count > 0)
            {
                logger.Info("Unable to close as workers still running");
                e.Cancel = true;
            }
            else
            {
                try
                {
                    foreach (BackgroundWorker item in workers)
                    {
                        item.Dispose();
                    }
                    //latestRestults.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Error("Error disposing :\n{0}", ex.Message);
                    Trace.TraceError(ex.Message);
                }
            }
        }

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                TextBoxHost.Text = ((MyResult)((ListBox)sender).SelectedValue).Hostname;
            }
            catch (Exception ex)
            {
                logger.Trace(ex);

                Trace.TraceError(ex.Message);
                TextBoxHost.Text = "";
            }
        }

        private void SearchResults_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBoxHost.Text = ((MyResult)((ListBox)sender)?.SelectedValue)?.Hostname ?? string.Empty;
            }
            catch (Exception ex)
            {
                logger.Trace(ex);
                Trace.TraceError(ex.Message);
            }
        }

        private void Connect_Button_Click(object sender, RoutedEventArgs e)
        {
            var startInfo = new ProcessStartInfo();
            var directory = Path.Combine(Environment.CurrentDirectory, "tools");
            const string rcExeToRun = @"d:\rc.exe";
            const string rcW10ExeToRun = @"\\national\bbcere\BSC\Dump\Apps\sccm-remote\w10\cmrcviewer.exe";
            var vncExeToRun = Path.Combine(Path.GetTempPath(), "vncx64.exe");


            switch (((Button)sender).Tag)
            {
                case "RDP":
                    startInfo.FileName = "cmd";
                    startInfo.Arguments =
                        $"/c runas /user:national\\{TextBoxEre.Text} /savecred \"mstsc.exe /v:{TextBoxHost.Text}\"";
                    break;

                case "RC7":
                    if (PrepareTool(Properties.Resources.rc, rcExeToRun))
                    {
                        startInfo.Arguments =
                            $"/c runas /user:national\\{TextBoxEre.Text} /savecred \"{rcExeToRun} 1 {TextBoxHost.Text.Trim()}\"";
                        startInfo.FileName = "cmd";
                    }
                    break;
                case "RC10":

                    startInfo.Arguments =
                        $"/c runas /user:national\\{TextBoxEre.Text} /savecred \"{rcW10ExeToRun} {TextBoxHost.Text.Trim()}\"";
                    startInfo.FileName = "cmd";

                    break;
                case "SSH":
                    startInfo.Arguments = $"{TextBoxHost.Text}";
                    startInfo.FileName = Path.Combine(directory, "putty.exe");
                    break;
                case "SSHERE":
                    startInfo.Arguments = $"{TextBoxEre.Text.Trim()}@{TextBoxHost.Text}";
                    startInfo.FileName = Path.Combine(directory, "putty.exe");
                    break;
                case "TELNET":
                    startInfo.Arguments = $"-telnet -P 23 {TextBoxHost.Text}";
                    startInfo.FileName = Path.Combine(directory, "putty.exe");
                    break;
                case "VNC":
                    if (PrepareTool(Properties.Resources.vncx64, vncExeToRun))
                    {
                        startInfo.Arguments = $"-username {TextBoxEre.Text} \"{TextBoxHost.Text.Trim()}\"";
                        startInfo.FileName = vncExeToRun;
                    }
                    break;
                case "HTTP":
                    startInfo.FileName = $"http://{TextBoxHost.Text.Trim()}:80/";
                    break;
                case "HTTPS":
                    startInfo.FileName = $"https://{TextBoxHost.Text.Trim()}:443/";
                    break;
                case "LOGVIEW":
                    string[] logViewPaths =
                    {
                        @"C:\Program Files (x86)\dira\diraBasics\LogView.exe",
                        @"C:\Program Files\dira\diraBasics\LogView.exe",
                        @"C:\Program Files\VCS\dira\diraBasics\LogView.exe"
                    };
                    foreach (string item in logViewPaths)
                    {
                        if (!File.Exists(item)) continue;
                        startInfo.FileName = item;
                        startInfo.Arguments = $"/ho:{TextBoxHost.Text.Trim()}";
                        break;
                    }
                    break;
            }

            if (startInfo.FileName.Length > 0)
            {
                logger.Info("Starting: {0} with argumets {1}", startInfo.FileName, startInfo.Arguments);
                Process.Start(startInfo);

            }
            if (LvHistory.Items.Contains(TextBoxHost.Text.Trim()))
            {
                LvHistory.Items.RemoveAt(LvHistory.Items.IndexOf(TextBoxHost.Text.Trim()));
            }

            LvHistory.Items.Insert(0, TextBoxHost.Text.Trim());

            var tempHist = new List<string>();
            foreach (string item in LvHistory.Items)
            {
                tempHist.Add(item);
            }
            Settings.Default.history = string.Join(";", tempHist);
            Settings.Default.Save();


        }


        private bool PrepareTool(byte[] resource, string outputPath)
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
                    logger.Trace("Tool doesn't exist, writing out new file");
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



        private void TextBox_ere_TextChanged(object sender, TextChangedEventArgs e)
        {
            Settings.Default.ere = ((TextBox)sender).Text;
            Settings.Default.Save();
        }


        private void TextBox_host_TextChanged(object sender, TextChangedEventArgs e)
        {
            logger.ConditionalTrace("host text changed: {0}", TextBoxHost.Text.Trim());
            hostText = TextBoxHost.Text.Trim();
            hostTimer.Stop();
            hostTimer.Start();
        }

        private string searchText;

        private void Search_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            logger.Trace("search timer elapsed: {0}", searchText);
            searchTimer.Stop();
            var worker = new BackgroundWorker();
            worker.DoWork += Do_Search;
            worker.RunWorkerCompleted += DisplayResults;
            workers.Add(worker);
            worker.RunWorkerAsync(searchText);
        }


        private void Host_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            logger.Debug("host timer elapsed: {0}", hostText);
            hostTimer.Stop();
            var connectionWorker = new BackgroundWorker();
            connectionWorker.DoWork += Do_Test_Connection;
            connectionWorker.RunWorkerCompleted += Complete_Test_Connection;
            connectionWorkers.Add(connectionWorker);
            connectionWorker.RunWorkerAsync(argument: hostText);
        }

        private void Complete_Test_Connection(object sender, RunWorkerCompletedEventArgs e)
        {
            var con = (MyConnection)e.Result;
            logger.Trace("Conection test to {1} completed at {0}", con.Timestamp, con.Host);
            if (lastConnectionResult < con.Timestamp)
            {
                Dispatcher.Invoke(delegate
                {
                    logger.Trace("setting all buttons as per results");
                    ButtonRdp.IsEnabled = con.Rdp;
                    ButtonRc.IsEnabled = con.Rdp;
                    ButtonRcW10.IsEnabled = con.Rdp;
                    ButtonVnc.IsEnabled = con.Vnc;
                    ButtonSsh.IsEnabled = con.Ssh;
                    ButtonSshEre.IsEnabled = con.Ssh;
                    ButtonHttp.IsEnabled = con.Http;
                    ButtonHttps.IsEnabled = con.Https;
                    ButtonTelnet.IsEnabled = con.Telnet;
                    ButtonLogView.IsEnabled = con.DiraLogView;
                    lastConnectionResult = con.Timestamp;

                    ButtonRcW10.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    ButtonRc.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    ButtonRdp.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    try
                    {
                        if (selectedResult?.OperatingSystem == null) return;
                        if (selectedResult.OperatingSystem.Contains("Windows 10"))
                        {
                            ButtonRcW10.Style = (Style)FindResource("MaterialDesignRaisedAccentButton");
                        }
                        else if (selectedResult.OperatingSystem.Contains("Windows 7"))
                        {
                            ButtonRc.Style = (Style)FindResource("MaterialDesignRaisedAccentButton");
                        }
                        else if (selectedResult.OperatingSystem.Contains("Windows Server"))
                        {
                            ButtonRdp.Style = (Style)FindResource("MaterialDesignRaisedAccentButton");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                        //// throw;
                    }
                });
            }
        }

        private void Do_Test_Connection(object sender, DoWorkEventArgs e)
        {
            logger.Info("Testing connection to {0}", e.Argument.ToString());
            using (var con = new MyConnection())
            {
                con.Host = e.Argument.ToString();
                // If short don't test
                if (e.Argument.ToString().Length < 4)
                {
                    e.Result = con;
                    return;
                }
                const int timeout = 200;

                if (IsPortOpen(e.Argument.ToString(), 3389, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.Rdp = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 5900, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.Vnc = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 22, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.Ssh = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 23, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.Telnet = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 80, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.Http = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 443, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.Https = true;
                }
                if (IsPortOpen(e.Argument.ToString(), 5100, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.DiraLogView = true;
                }
                e.Result = con;
            }
        }

        private static bool IsPortOpen(string host, int port, TimeSpan timeout)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(timeout);
                    if (!success)
                    {
                        return false;
                    }
                    client.EndConnect(result);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void Load_Result_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (((Button)sender).Content.ToString() == "Load IP")
                {
                    var tempBw = new BackgroundWorker();
                    tempBw.DoWork += delegate
                        {
                            Dispatcher.Invoke(delegate
                            {
                                try
                                {
                                    TextBoxHost.Text = Dns.GetHostEntry(((Button)sender).Tag.ToString()).AddressList[0].ToString();
                                }
                                catch (Exception ex)
                                {
                                    Trace.TraceError(ex.Message);
                                    TextBoxHost.Text = "";
                                }
                            });

                        };
                    tempBw.RunWorkerAsync();
                }
                else
                {
                    TextBoxHost.Text = ((Button)sender).Content.ToString();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                TextBoxHost.Text = "";
            }
        }

        private void LvHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (null != ((ListView)sender).SelectedValue)
            {
                TextBoxHost.Text = ((ListView)sender).SelectedValue.ToString();
            }

        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).Header = "Click to close help";
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).Header = "Click to open help";

        }

        private void Button_Phonebox_Click(object sender, RoutedEventArgs e)
        {
            logger.Info("Phonebox button pressed with content {0}", ((Button)sender).Content);

            if (!File.Exists(PhoneboxIniPath))
            {
                logger.Error("Phonebox ini file doesn't exist at {0}. Not switching.", PhoneboxIniPath);
                return;
            }
            if (Process.GetProcessesByName("PhoneBOX.Client").Count() != 0)
            {
                logger.Warn("Phonebox running, will not continue.");
                MessageBox.Show(messageBoxText: "Close PhoneBOX before continuing.", caption: "ERROR", button: MessageBoxButton.OK, icon: MessageBoxImage.Warning);
                return;
            }

            var phoneBoxConfig = new PhoneBoxConfig();
            // TODO make configuration
            switch (((Button)sender).Content)
            {
                case "West":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBW01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBW02";
                    phoneBoxConfig.OasisAddress = "3GBV2APOAS1002";
                    phoneBoxConfig.OasisBackupAddress = "3GBV1APOAS1002";
                    break;

                case "South":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBS01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBS02";
                    phoneBoxConfig.OasisAddress = "3GBV2APOAS1002";
                    phoneBoxConfig.OasisBackupAddress = "3GBV1APOAS1002";
                    break;

                case "North":
                    phoneBoxConfig.ServerAddress = "3GBV1APPBXBN01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV2APPBXBN2";
                    phoneBoxConfig.OasisAddress = "3GBV1APOAS1001";
                    phoneBoxConfig.OasisBackupAddress = "3GBV2APOAS1001";
                    break;

                case "Midlands":
                    phoneBoxConfig.ServerAddress = "3GBV1APPBXBM01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV2APPBXBM02";
                    phoneBoxConfig.OasisAddress = "3GBV1APOAS1001";
                    phoneBoxConfig.OasisBackupAddress = "3GBV2APOAS1001";
                    break;

                case "East":
                    phoneBoxConfig.ServerAddress = "3GBV2APPBXBE01";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBXBE02";
                    phoneBoxConfig.OasisAddress = "3GBV2APOAS1002";
                    phoneBoxConfig.OasisBackupAddress = "3GBV1APOAS1002";
                    break;

                case "VTS":
                    phoneBoxConfig.ServerAddress = "3GBV1APPBX6001"; // "10.32.13.220";
                    phoneBoxConfig.ServerBackupAddress = "3GBV1APPBX6002";// "10.32.13.221";
                    phoneBoxConfig.OasisAddress = "3GBV1APOAS6001"; // "10.32.13.222";
                    phoneBoxConfig.OasisBackupAddress = "3GBV1APOAS6002";
                    break;

                default:
                    logger.Error("Unknonw phonebox site given");
                    phoneBoxConfig = null;
                    break;
            }

            if (phoneBoxConfig == null) return;
            logger.Debug("Writing config to {0}\n{1}", PhoneboxIniPath, phoneBoxConfig);
            try
            {
                File.WriteAllLines(PhoneboxIniPath, phoneBoxConfig.ToStringArray());
                logger.Info("Attempting to start Phonebox");
                try
                {
                    Process.Start(PhoneboxExePath);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Problem starting PhoneBOX");
                    App.SendReport(ex);
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType() == typeof(UnauthorizedAccessException))
                {
                    MessageBox.Show($"Check file permissions for {PhoneboxIniPath}", "Problem writing configuration", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                logger.Error(ex, "Problem writing PhoneBOX ini file - check file permission.");
                App.SendReport(ex);
            }
        }

        private void TextBox_host_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TextBoxHost.IsEnabled = !TextBoxHost.IsEnabled;
        }


     
       
    }
}
