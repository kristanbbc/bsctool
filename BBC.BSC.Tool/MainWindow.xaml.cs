using Newtonsoft.Json;
using NLog;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
//using System.Threading;

namespace BBC.BSC.Tool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //TODO make more of these configurable?
        private readonly string catPath = @"http://er.bbc.co.uk/catquery.php?json&query=";
        private SearchResultCollection latestRestults;
        private List<BackgroundWorker> workers = new List<BackgroundWorker>();
        private List<BackgroundWorker> connectionWorkers = new List<BackgroundWorker>();
        private DateTime LastConnectionResult = DateTime.Now;
        private DateTime lastResultTimestamp;
        private readonly string ldapPath = @"LDAP://ldap.national.core.bbc.co.uk";
        private Timer searchTimer = new Timer(400);
        private Timer hostTimer = new Timer(400);
        private string host_text;
        private Logger logger;
#if !DEBUG
        private MailTarget mailTarget;
        private MemoryTarget memoryTarget;
#endif

        private string phoneboxIniPath = @"C:\ProgramData\Broadcast Bionics\PhoneBOX4\client.ini";
        private string phoneboxExePath = @"C:\Program Files (x86)\Broadcast Bionics\PhoneBOX4\Client\PhoneBOX.Client.exe";
        public MainWindow()
        {
            InitializeComponent();
            NLog.Config.LoggingConfiguration config = new NLog.Config.LoggingConfiguration();
            ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget
            {
                Layout = "${time} ${pad:padding=3:inner=${threadid}} ${message} ${exception:format=tostring}"
            };
#if !DEBUG
            mailTarget = new MailTarget()
            {
                To = "kristan.webb@bbc.co.uk",
                From = string.Format("{0}-{1}-bsctool@bbc.co.uk", Environment.UserName, Environment.MachineName),
                SmtpServer = "smtp.national.core.bbc.co.uk"
            };
            memoryTarget = new MemoryTarget();
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, memoryTarget);
            config.AddRule(LogLevel.Warn, LogLevel.Fatal, mailTarget);
#endif

            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);

            NLog.LogManager.Configuration = config;
            logger = LogManager.GetCurrentClassLogger();

            logger.Info("BSC Tool {0} starting.", FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).ProductVersion);
            System.Timers.Timer watcher = new System.Timers.Timer
            {
                Interval = 1000
            };
            watcher.Elapsed += Do_Watcher;
            watcher.Enabled = true;
            System.Threading.ThreadPool.GetMinThreads(out int w, out int c);

            // Write the numbers of minimum threads
            logger.Debug("Minumium number of threads available {0}, {1}", w, c);

            System.Threading.ThreadPool.SetMinThreads(20, 10);
            hostTimer.Elapsed += Host_Timer_Elapsed;
            searchTimer.Elapsed += Search_Timer_Elapsed;
            DataContext = this;
            searchResults.ItemsSource = Results;


            foreach (string item in Properties.Settings.Default.history.Split(';'))
            {
                if (item == "System.Windows.Controls.ItemCollection")
                {
                    continue;
                }
                lvHisotry.Items.Add(item);
            }


            //If PhoneBox not installed don't enable tab
            if (!File.Exists(phoneboxExePath))
            {
                PhoneBoxSwitcherTab.IsEnabled = false;
                foreach (Button item in UIHelper.FindVisualChildren<Button>(PhoneBoxButtons))
                {
                    item.IsEnabled = false;
                }
            }

            BuildWs600View();
            // Put Cursor in search box.
            searchIn.Focus();
        }


        private void BuildWs600View()
        {
            foreach (string item in Directory.GetDirectories(@"\\ws600\vnc\"))
            {
                logger.ConditionalTrace("Adding directory {0} to BNCS tree", item);
                TreeViewItem treeViewItem = new TreeViewItem
                {
                    Header = Path.GetFileNameWithoutExtension(item),
                    Tag = item
                };
                treeViewItem.Items.Add(null);
                treeViewItem.Expanded += new RoutedEventHandler(TreeViewBNCS_Expanded);

                treeViewBNCS.Items.Add(treeViewItem);

            }
            foreach (string item in Directory.GetFiles(@"\\ws600\vnc\"))
            {
                logger.ConditionalTrace("Adding file {0} to BNCS tree", item);
                TreeViewItem treeViewItem = new TreeViewItem
                {
                    Header = Path.GetFileNameWithoutExtension(item),
                    Tag = item
                };
                treeViewItem.MouseDoubleClick += TreeViewBNCS_DoubleClicked;
                treeViewBNCS.Items.Add(treeViewItem);
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
                    string VncExeToRun = Path.Combine(Path.GetTempPath(), "vncx64.exe");
                    if (PrepareTool(Properties.Resources.vncx64, VncExeToRun))
                    {
                        startInfo.Arguments = string.Format(@" ""{0}"" ", tvSender.Tag.ToString());
                        startInfo.FileName = VncExeToRun;
                    }

                    break;
                case "url":
                    startInfo.FileName = string.Format("{0}", tvSender.Tag.ToString());


                    break;
                default:
                    startInfo.FileName = string.Format("{0}", tvSender.Tag.ToString());

                    break;
            }

            if (startInfo.FileName.Length > 0)
            {
                logger.Info("Starting: {0} with argumets {1}", startInfo.FileName, startInfo.Arguments);
                Process proc = Process.Start(startInfo);

            }

        }

        private void TreeViewBNCS_Expanded(object sender, RoutedEventArgs e)
        {

            TreeViewItem tvSender = (TreeViewItem)sender;
            if (tvSender.Items.Count == 1 && tvSender.Items[0] == null)
            {
                tvSender.Items.Clear();

                foreach (string item in Directory.GetDirectories(tvSender.Tag.ToString()))
                {
                    logger.ConditionalTrace("Adding directory {0} to BNCS tree", item);
                    TreeViewItem treeViewItem = new TreeViewItem
                    {
                        Header = Path.GetFileNameWithoutExtension(item),
                        Tag = item
                    };

                    treeViewItem.Items.Add(null);
                    treeViewItem.Expanded += new RoutedEventHandler(TreeViewBNCS_Expanded);

                    tvSender.Items.Add(treeViewItem);

                }
                foreach (string item in Directory.GetFiles(tvSender.Tag.ToString()))
                {
                    logger.ConditionalTrace("Adding file {0} to BNCS tree", item);
                    TreeViewItem treeViewItem = new TreeViewItem
                    {
                        Header = Path.GetFileNameWithoutExtension(item),
                        Tag = item
                    };
                    treeViewItem.MouseDoubleClick += TreeViewBNCS_DoubleClicked;

                    tvSender.Items.Add(treeViewItem);
                }
            }
        }


        /// <summary>Updates the status box if running searches are still happening.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Do_Watcher(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(delegate ()
            {
                if (workers.Count > 0)
                {
                    logger.Debug("There are {0} workers", workers.Count);
                    status.Fill = new SolidColorBrush(Colors.Red);
                    // this.Title = "Busy";
                }
                else
                {
                    status.Fill = new SolidColorBrush(Colors.Green);
                    //this.Title = "Finished";
                }
            });

        }

        private List<CatRestult> catRestults = new List<CatRestult>();
        private class CatRestult
        {
            [JsonProperty("host_name")]
            public string HostName;
            [JsonProperty("also_known_as")]
            public string AlsoKnownAs;
            [JsonProperty("ip")]
            public string IP;

        }

        private void Do_Search(object sender, DoWorkEventArgs e)
        {
            Dispatcher.Invoke(delegate ()
            {
                status.Fill = new SolidColorBrush(Colors.Red);
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
                    string catQuery = string.Format("{1}SELECT host_name, also_known_as, CAST(inet_ntoa(ip) as CHAR(15)) as ip FROM " +
                        "network INNER JOIN asset ON network.asset_id = asset.asset_id " +
                        "WHERE life_cycle_status_id = 4 AND (lower(host_name like '%{0}%') OR IP = inet_aton('{0}') OR lower(also_known_as) LIKE '%{0}%')",
                        e.Argument.ToString().Replace("*", "%").ToLower(),
                        catPath);

                    string json_data = string.Empty;
                    logger.Debug("Running query against CAT with\n{0}", catQuery);
                    using (WebClient w = new WebClient())
                    {
                        w.UseDefaultCredentials = true;
                        json_data = w.DownloadString(catQuery);
                    }

                    catRestults = !string.IsNullOrEmpty(json_data) ? JsonConvert.DeserializeObject<List<CatRestult>>(json_data) : null;
                    logger.Debug("Got {0} results from CAT", catRestults.Count());
                    foreach (CatRestult item in catRestults)
                    {
                        results.results.Add(new MyResult
                        {
                            Source = "CAT",
                            Hostname = item.HostName.ToUpper(),
                            Description = item.AlsoKnownAs,
                            Ip = item.IP
                        });
                    }


                }
                catch (Exception ex)
                {
                    logger.Warn("Error running query against CAT:\n{0}", ex.Message);
                    Trace.TraceError(ex.Message);
                }
                try
                {
                    using (DirectoryEntry dEntry = new DirectoryEntry(ldapPath))
                    using (DirectorySearcher dSearcher = new DirectorySearcher(dEntry)
                    {
                        // (|(cn=*334810*)(displayname=*334810*)(cn=PC-*334810*)(cn=B1-D0*334810*)(cn=B1-L0*334810*)(cn=61-D0*334810*)(cn=61-L0*334810*)(cn=71-D0*334810*)(cn=71-L0*334810*)(cn=91-D0*334810*)(cn=91-L0*334810*)(cn=F1-D0*334810*)(cn=F1-L0*334810*)(cn=MC-*334810*)(sn=*334810*)(samAccountName=*334810*)(mail=*334810*)(proxyaddresses=smtp:*334810*)(ou=*334810*)(&(objectcategory=printqueue)(printername=*334810*)))
                        //Filter = string.Format("(&(objectClass=computer)(cn={0}*))", e.Argument.ToString()),
                        Filter = string.Format("(&(!userAccountControl:1.2.840.113556.1.4.803:=2)(objectClass=computer)(|(cn={0}*)(displayname={0}*)(cn=PC-{0}*)(cn=B1-D0{0}*)(cn=B1-L0{0}*)(cn=31-D0{0}*)(cn=31*-D0{0}*)(cn=61-D0{0}*)(cn=61-L0{0}*)(cn=71-D0{0}*)(cn=71-L0{0}*)(cn=91-D0{0}*)(cn=91-L0{0}*)(cn=F1-D0{0}*)(cn=F1-L0{0}*)(cn=MC-{0}*)(sn={0}*)(samAccountName={0}*)))", e.Argument.ToString()),
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
                        using (SearchResultCollection sResults = dSearcher.FindAll())
                        {
                            latestRestults = sResults;
                            logger.Info("Found {0} results in Active Directory", sResults.Count);
                            foreach (SearchResult item in sResults)
                            {
                                logger.ConditionalTrace("AD: found: {0}", item.Properties["name"][0].ToString().ToUpper());
                                if (!results.results.Any(n => n.Hostname == item.Properties["name"][0].ToString().ToUpper()))
                                {
                                    results.AddResult(new MyResult()
                                    {
                                        Hostname = item.Properties["name"][0].ToString().ToUpper(),
                                        Description = (item.Properties.Contains("description") ? item.Properties["description"][0].ToString() : ""),
                                        Ip = "Load IP",
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
            results.results.Sort((a, b) => a.Hostname.CompareTo(b.Hostname));
            results.results = results.results.Distinct().ToList<MyResult>();
            e.Result = results;
        }

        private void Text_Changed(object sender, TextChangedEventArgs e)
        {
            logger.ConditionalTrace("search text changed: {0}", searchIn.Text.Trim());
            search_text = searchIn.Text;
            searchTimer.Stop();
            searchTimer.Start();
            textbox_host.Text = searchIn.Text;
        }

        public List<MyResult> Results;

        private void DisplayResults(object sender, RunWorkerCompletedEventArgs e)
        {
            workers.Remove((BackgroundWorker)sender);
            ((BackgroundWorker)sender).Dispose();
            MyResults res = (MyResults)e.Result;
            try
            {
                if (res.timestamp > lastResultTimestamp)
                {
                    Dispatcher.Invoke(delegate ()
                    {
                        ObservableCollection<MyResult> Results = new ObservableCollection<MyResult>();
                        foreach (MyResult item in res.results)
                        {
                            Results.Add(item);
                        }
                        searchResults.ItemsSource = null;
                        searchResults.ItemsSource = Results;

                        if (res.results.Count == 1)
                        {
                            textbox_host.Text = res.results[0].Hostname;
                        }
                        //searchResults.Items.Refresh();
                    });
                    lastResultTimestamp = res.timestamp;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                logger.Error("Problem displaying results:\n{0}", ex.Message);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Save();


            if (workers.Count > 0)
            {
                logger.Info("Unable to close as workers still running");
                e.Cancel = true;
                return;
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

#if !DEBUG
                //send logs as email
                try
                {

                    SmtpClient smtpClient = new SmtpClient
                    {
                        Host = mailTarget.SmtpServer.ToString()
                    };
                    MailMessage mailMessage = new MailMessage();
                    mailMessage.To.Add(mailTarget.To.ToString());
                    mailMessage.From = new MailAddress(mailTarget.From.ToString());
                    mailMessage.Subject = "Full logs from BSC Tool";
                    mailMessage.Body = string.Join(Environment.NewLine, memoryTarget.Logs);
                    smtpClient.Send(mailMessage);


                }
                catch (Exception ex)
                {
                    logger.Warn(ex);
                    //temp fix to ensure email is sent
                    logger.Warn(string.Join(Environment.NewLine, memoryTarget.Logs));
                }

#endif
            }
        }

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                textbox_host.Text = ((MyResult)((ListBox)sender).SelectedValue).Hostname.ToString();
            }
            catch (Exception ex)
            {
                logger.Trace(ex);

                Trace.TraceError(ex.Message);
                textbox_host.Text = "";
            }
        }

        private void SearchResults_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                textbox_host.Text = ((MyResult)((ListBox)sender).SelectedValue).Hostname.ToString();
            }
            catch (Exception ex)
            {
                logger.Trace(ex);

                Trace.TraceError(ex.Message);
            }
        }

        private void Connect_Button_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();

            string directory = System.IO.Path.Combine(Environment.CurrentDirectory, "tools");

            switch (((Button)sender).Name)
            {

                case "button_RDP":
                    startInfo.FileName = "cmd";
                    startInfo.Arguments = string.Format(@"/c runas /user:national\{1} /savecred ""mstsc.exe /v:{0}""", textbox_host.Text, textBox_ere.Text);
                    break;

                case "button_RC":
                    string exeToRun = @"d:\rc.exe";
                    if (PrepareTool(Properties.Resources.rc, exeToRun))
                    {
                        startInfo.Arguments = string.Format(@"/c runas /user:national\{0} /savecred ""{1} 1 {2}""", textBox_ere.Text, exeToRun, textbox_host.Text.Trim());
                        startInfo.FileName = "cmd";
                    }
                    break;
                case "button_SSH":
                    startInfo.Arguments = string.Format("{0}", textbox_host.Text);
                    startInfo.FileName = System.IO.Path.Combine(directory, "putty.exe");
                    break;
                case "button_SSH_ERE":
                    startInfo.Arguments = string.Format("{1}@{0}", textbox_host.Text, textBox_ere.Text.Trim());
                    startInfo.FileName = System.IO.Path.Combine(directory, "putty.exe");
                    break;
                case "button_TELNET":
                    startInfo.Arguments = string.Format("-telnet -P 23 {0}", textbox_host.Text);
                    startInfo.FileName = System.IO.Path.Combine(directory, "putty.exe");
                    break;
                case "button_VNC":
                    string VncExeToRun = Path.Combine(Path.GetTempPath(), "vncx64.exe");
                    if (PrepareTool(Properties.Resources.vncx64, VncExeToRun))
                    {
                        startInfo.Arguments = string.Format(@"-username {0} ""{1}""", textBox_ere.Text, textbox_host.Text.Trim());
                        startInfo.FileName = VncExeToRun;
                    }
                    break;
                case "button_HTTP":
                    startInfo.FileName = string.Format("http://{0}:80/", textbox_host.Text.Trim());
                    break;
                case "button_HTTPS":
                    startInfo.FileName = string.Format("https://{0}:443/", textbox_host.Text.Trim());
                    break;
                case "button_LogView":
                    string[] logViewPaths =
                    {
                        @"C:\Program Files (x86)\dira\diraBasics\LogView.exe",
                        @"C:\Program Files\dira\diraBasics\LogView.exe",
                        @"C:\Program Files\VCS\dira\diraBasics\LogView.exe"
                    };
                    foreach (string item in logViewPaths)
                    {
                        if (File.Exists(item))
                        {
                            startInfo.FileName = item;
                            startInfo.Arguments = string.Format("/ho:{0}", textbox_host.Text.Trim());
                            break;
                        }
                    }



                    break;
                default:
                    break;
            }

            if (startInfo.FileName.Length > 0)
            {
                logger.Info("Starting: {0} with argumets {1}", startInfo.FileName, startInfo.Arguments);
                Process proc = Process.Start(startInfo);

            }
            if (!lvHisotry.Items.Contains(textbox_host.Text.Trim()))
            {
                lvHisotry.Items.Insert(0, textbox_host.Text.Trim());

                List<string> tempHist = new List<string>();
                foreach (string item in lvHisotry.Items)
                {
                    tempHist.Add(item);
                }
                Properties.Settings.Default.history = String.Join(";", tempHist);
                Properties.Settings.Default.Save();
            }

        }


        private bool PrepareTool(byte[] resource, string outputPath)
        {
            logger.Trace("Preparing tool to path {0}", outputPath);
            byte[] existingMD5;
            byte[] resourceMD5;
            if (System.IO.File.Exists(outputPath))
            {
                logger.Trace("Tool path already exists.");
                //check md5
                using (MD5 md5 = MD5.Create())
                {
                    using (System.IO.FileStream stream = System.IO.File.OpenRead(outputPath))
                    {
                        existingMD5 = md5.ComputeHash(stream);
                    }
                }

                //md5 of embedded resource
                using (MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    md5.TransformFinalBlock(resource, 0, resource.Length);
                    resourceMD5 = md5.Hash;
                }

                if (System.Text.Encoding.Default.GetString(existingMD5) == System.Text.Encoding.Default.GetString(resourceMD5))
                {
                    logger.Trace("Tool path exists and MD5 matches, returning true");
                    return true;
                }
                else
                {
                    logger.Warn("Tool path exists, but MD5 doesn't match, returning false");
                    return false;
                }

            }
            else
            {
                logger.Trace("Tool doesn't exist, writing out new file");
                using (System.IO.FileStream exeFile = new System.IO.FileStream(outputPath, System.IO.FileMode.Create))
                {
                    exeFile.Write(resource, 0, resource.Length);
                }
                logger.Debug("Tool written to {0}, returning true", outputPath);
                return true;
            }
            return false;
        }



        private void TextBox_ere_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.ere = ((TextBox)sender).Text;
            Properties.Settings.Default.Save();
        }


        private void Textbox_host_TextChanged(object sender, TextChangedEventArgs e)
        {
            logger.ConditionalTrace("host text changed: {0}", textbox_host.Text.Trim());
            host_text = textbox_host.Text.Trim();
            hostTimer.Stop();
            hostTimer.Start();
        }

        private string search_text;

        private void Search_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            logger.Trace("search timer elapsed: {0}", search_text);
            searchTimer.Stop();
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Do_Search;
            worker.RunWorkerCompleted += DisplayResults;
            workers.Add(worker);
            worker.RunWorkerAsync(search_text);
        }


        private void Host_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            logger.Debug("host timer elapsed: {0}", host_text);
            hostTimer.Stop();
            BackgroundWorker connectionWorker = new BackgroundWorker();
            connectionWorker.DoWork += Do_Test_Connection;
            connectionWorker.RunWorkerCompleted += Complete_Test_Connection;
            connectionWorkers.Add(connectionWorker);
            connectionWorker.RunWorkerAsync(argument: host_text);
        }

        private void Complete_Test_Connection(object sender, RunWorkerCompletedEventArgs e)
        {
            MyConnection con = (MyConnection)e.Result;
            if (LastConnectionResult < con.timestamp)
            {
                Dispatcher.Invoke(delegate ()
                {
                    button_RDP.IsEnabled = con.rdp;
                    button_RC.IsEnabled = con.rdp;
                    button_VNC.IsEnabled = con.vnc;
                    button_SSH.IsEnabled = con.ssh;
                    button_SSH_ERE.IsEnabled = con.ssh;
                    button_HTTP.IsEnabled = con.http;
                    button_HTTPS.IsEnabled = con.https;
                    button_TELNET.IsEnabled = con.telnet;
                    button_LogView.IsEnabled = con.diralogview;
                    LastConnectionResult = con.timestamp;
                });
            }



        }

        private void Do_Test_Connection(object sender, DoWorkEventArgs e)
        {

            logger.Info("Testing connection to {0}", e.Argument.ToString());
            using (MyConnection con = new MyConnection())
            {
                if (e.Argument.ToString().Length < 4)
                {
                    e.Result = con;
                    return;
                }
                int timeout = 100;

                if (IsPortOpen(e.Argument.ToString(), 3389, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.rdp = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 5900, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.vnc = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 22, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.ssh = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 23, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.telnet = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 80, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.http = true;
                }

                if (IsPortOpen(e.Argument.ToString(), 443, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.https = true;
                }
                if (IsPortOpen(e.Argument.ToString(), 5100, TimeSpan.FromMilliseconds(timeout)))
                {
                    con.diralogview = true;
                }

                e.Result = con;
            }


        }

        private bool IsPortOpen(string host, int port, TimeSpan timeout)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult result = client.BeginConnect(host, port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(timeout);
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

                    BackgroundWorker tempBw = new BackgroundWorker();
                    tempBw.DoWork += delegate
                        {
                            Dispatcher.Invoke(delegate ()
                        {
                            try
                            {
                                textbox_host.Text = System.Net.Dns.GetHostEntry(((Button)sender).Tag.ToString()).AddressList[0].ToString();
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError(ex.Message);
                                textbox_host.Text = null;
                            }

                        });

                        };
                    tempBw.RunWorkerAsync();



                }
                else
                {
                    textbox_host.Text = ((Button)sender).Content.ToString();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                textbox_host.Text = "";
            }



        }

        private void LvHisotry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            textbox_host.Text = e.AddedItems[0].ToString();
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
            logger.Info("Phonbox button pressed with content {0}", ((Button)sender).Content);

            if (!File.Exists(phoneboxIniPath))
            {
                logger.Error("Phonebox ini file doesn't exist at {0}. Not switching.", phoneboxIniPath);
                return;
            }
            if (Process.GetProcessesByName("PhoneBOX.Client").Count() != 0)
            {
                logger.Warn("Phonebox running, will not continue.");
                MessageBox.Show(messageBoxText: "Close PhoneBOX before continuing.", caption: "ERROR", button: MessageBoxButton.OK, icon: MessageBoxImage.Warning);
                return;
            }

            PhoneBoxConfig phoneBoxConfig = new PhoneBoxConfig();
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




            if (phoneBoxConfig != null)
            {
                logger.Debug("Writing config to {0}\n{1}", phoneboxIniPath, phoneBoxConfig);
                File.WriteAllLines(phoneboxIniPath, phoneBoxConfig.ToStringArray());

                logger.Info("Attempting to start Phonebox");
                try
                {
                    Process proc = Process.Start(phoneboxExePath);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Problem starting PhoneBOX");
                }



            }
        }

        private void Textbox_host_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (textbox_host.IsEnabled == true)
            {
                textbox_host.IsEnabled = false;
            }
            else
            {
                textbox_host.IsEnabled = true;
            }
        }
    }
    internal class PhoneBoxConfig
    {
        public string ServerAddress { get; set; }
        public string ServerBackupAddress { get; set; }
        public string OasisAddress { get; set; }
        public string OasisBackupAddress { get; set; }

        public override string ToString()
        {
            return string.Format(@"[server]
backupaddress = {1}
address = {0}
[oasis]
address = {2}
backupaddress = {3}
",
                ServerAddress,
                ServerBackupAddress,
                OasisAddress,
                OasisBackupAddress);
        }
        public string[] ToStringArray()
        {
            return new string[] {
                $"[server]",
                $"backupaddress = {ServerBackupAddress}",
                $"address = {ServerAddress}",
                $"[oasis]",
                $"address = {OasisAddress}",
                $"backupaddress = {OasisBackupAddress}"
            };

        }
    }

    internal class MyResults
    {

        public DateTime timestamp
        {
            get;
        }
        public List<MyResult> results = new List<MyResult>();

        public MyResults()
        {
            timestamp = DateTime.Now;
        }
        public void AddResult(MyResult result)
        {
            results.Add(result);
        }



    }
    public class MyResult
    {
        public string Source
        {
            get;
            set;
        }
        public string Hostname
        {
            get;
            set;
        }
        public string Ip
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }
        public MyResult()
        {

        }
        public MyResult(string HOSTNAME, string SOURCE = null)
        {
            Hostname = HOSTNAME;
            Source = SOURCE;
        }
        public MyResult(string HOSTNAME, string IP, string SOURCE = null)
        {
            Hostname = HOSTNAME;
            Ip = IP;
            Source = SOURCE;
        }

    };
    internal class MyConnection : IDisposable
    {
        public DateTime timestamp = DateTime.Now;
        public bool rdp = false;
        public bool vnc = false;
        public bool ssh = false;
        public bool http = false;
        public bool https = false;
        public bool telnet = false;
        public bool diralogview = false;

        public void Dispose()
        {
            //    this.rdp = null;
            //    this.vnc = null;
            //    this.ssh = null;
            //    this.http = null;
            //    this.telnet = null;
        }
    }


    public class SettingBindingExtension : Binding
    {
        public SettingBindingExtension()
        {
            Initialize();
        }

        public SettingBindingExtension(string path)
            : base(path)
        {
            Initialize();
        }

        private void Initialize()
        {
            Source = Properties.Settings.Default;
            Mode = BindingMode.TwoWay;
        }
    }

    public static class UIHelper
    {

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                T childType = child as T;
                if (childType != null)
                {
                    yield return (T)child;
                }

                foreach (T other in FindVisualChildren<T>(child))
                {
                    yield return other;
                }
            }
        }

    }
}
