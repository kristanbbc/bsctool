using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BBC.BSC.Tool.Properties;
using Newtonsoft.Json;
using NLog;
using Timer = System.Timers.Timer;

namespace BBC.BSC.Tool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        //TODO make more of these configurable?
        private const string CatPath = @"http://cat.er.bbc.co.uk/catquery.php?json&query="; //DevSkim: ignore DS137138 until 2021-08-05

        private readonly List<BackgroundWorker> workers = new List<BackgroundWorker>();
        // ReSharper disable once CollectionNeverQueried.Local
        private readonly List<BackgroundWorker> connectionWorkers = new List<BackgroundWorker>();
        private DateTime lastConnectionResult = DateTime.Now;
        private DateTime lastResultTimestamp;
        private const string LdapPath = @"LDAP://ldap.national.core.bbc.co.uk";
        private readonly Timer searchTimer = new Timer(400);
        private readonly Timer hostTimer = new Timer(400);
        private string hostText;
        private readonly Logger logger;

        private const string PhoneboxIniPath = @"C:\ProgramData\Broadcast Bionics\PhoneBOX4\client.ini";
        private const string PhoneboxExePath = @"C:\Program Files (x86)\Broadcast Bionics\PhoneBOX4\Client\PhoneBOX.Client.exe";


        public static VCenter.VmList.Root cachedVcenter;

        public MainWindow()
        {
            InitializeComponent();

            Title = "BSC Tool - Version " + Assembly.GetExecutingAssembly().GetName().Version;

            logger = new Logging().initLogger();

            logger.Info("BSC Tool {0} starting.", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            Timer watcher = new Timer
            {
                Interval = 1000
            };
            watcher.Elapsed += Do_Watcher;
            watcher.Enabled = true;
            ThreadPool.GetMinThreads(out int w, out int c);

            // Write the numbers of minimum threads
            logger.Debug("Minumium number of threads available {0}, {1}", w, c);

            _ = ThreadPool.SetMinThreads(20, 10);
            hostTimer.Elapsed += Host_Timer_Elapsed;
            searchTimer.Elapsed += Search_Timer_Elapsed;
            DataContext = this;

            Settings.Default.history.Split(';').Where(h => h != "System.Windows.Controls.ItemCollection").ToList().ForEach(x => LvHistory.Items.Add(x));
            //foreach (string item in Settings.Default.history.Split(';').Where(h => h != "System.Windows.Controls.ItemCollection"))
            //{
            //    _ = LvHistory.Items.Add(item);
            //}

            //If PhoneBox not installed don't enable tab
            if (!File.Exists(PhoneboxExePath))
            {
                PhoneBoxSwitcherTab.IsEnabled = false;
                UiHelper.FindVisualChildren<Button>(PhoneBoxButtons).ToList().ForEach(x => x.IsEnabled = false);
            }
            //if (Directory.Exists(BncsDir))
            //{
            //    Dispatcher.Invoke(BuildWs600View);
            //}
            //else
            //{
            //    TabItemBncsVnc.IsEnabled = false;
            //}

            //Dispatcher.Invoke(delegate
            //{
            //    UpdateInfoGridAllocation();
            //});

            // Put Cursor in search box.
            _ = SearchIn.Focus();
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
            catch (TaskCanceledException ex)
            {
                logger.Error("Application closed whilst task still in progress", ex);
            }
            catch
            {
                throw;
            }
        }

        private List<Modules.CatResult> catResults = new List<Modules.CatResult>();



        private void Do_Search(object sender, DoWorkEventArgs e)
        {
            Dispatcher.Invoke(delegate
            {
                Status.Fill = new SolidColorBrush(Colors.Red);
            });
            Results.MyResults results = new Results.MyResults();
            if (e.Argument.ToString().Length < 4)
            {
                e.Result = null;
            }
            else
            {
                logger.Info("{1} DoSearch: {0}", e.Argument.ToString(), Environment.CurrentManagedThreadId);

                if (null != cachedVcenter)
                {
                    logger.Info("loading cached vCenter results ({0} total)", cachedVcenter.Value.Count);

                    try
                    {
                        foreach (var item in cachedVcenter.Value)
                        {
                           //logger.Trace("vCenter item being tested {0} contains {1}", item.Name, e.Argument.ToString());
                            if (item.Name.ToLower().Contains(e.Argument.ToString()))
                            {
                                logger.Trace("vCenter Cache - adding {0} to results list", item.Name);
                                results.Results.Add(new Results.MyResult { Hostname = item.Name, Source = "vCenter", Tag = item.Vm, Description = "From vCenter" });
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                        logger.Warn("Error running query against vCenter:\n{0}", ex.Message);
                        Trace.TraceError(ex.Message);
                    }


                }


                try
                {
                    string catQuery =
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
                        catResults = JsonConvert.DeserializeObject<List<Modules.CatResult>>(jsonData);
                        logger.Info("Got {0} results from CAT", catResults?.Count);

                        if (catResults != null)
                            foreach (var item in catResults)
                            {
                                results.Results.Add(new Results.MyResult
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
                        _ = dSearcher.PropertiesToLoad.Add("name");
                        _ = dSearcher.PropertiesToLoad.Add("description");
                        _ = dSearcher.PropertiesToLoad.Add("operatingsystem");
                        using (var sResults = dSearcher.FindAll())
                        {
                            logger.Info("Found {0} results in Active Directory", sResults.Count);
                            foreach (SearchResult item in sResults)
                            {
                                logger.ConditionalTrace("AD: found: {0}", item.Properties["name"][0].ToString().ToUpper());
                                if (results.Results.All(n => n.Hostname != item.Properties["name"][0].ToString().ToUpper()))
                                {
                                    results.AddResult(new Results.MyResult
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
                catch (InvalidOperationException ex)
                {
                    logger.Warn("Invalid Operation querying AD: ", ex);
                }
                catch (NotSupportedException ex)
                {
                    logger.Warn("LDAP query error: {0}", ex.Message);
                }
                catch
                {
                    throw;
                }
            }
            results.Results.Sort((a, b) => string.Compare(a.Hostname, b.Hostname, StringComparison.Ordinal));
            results.Results = results.Results.Distinct().ToList();
            e.Result = results;
        }

        private static string CleanResultProperty(SearchResult item,
                                                  string property) => item.Properties.Contains(property) ? item.Properties[property][0].ToString() : "";

        private void Text_Changed(object sender, TextChangedEventArgs e)
        {
            logger.ConditionalTrace("search text changed: {0}", SearchIn.Text.Trim());
            searchText = SearchIn.Text;
            searchTimer.Stop();
            searchTimer.Start();
            TextBoxHost.Text = SearchIn.Text;
        }

        private Results.MyResult selectedResult = new Results.MyResult();

        private void DisplayResults(object sender, RunWorkerCompletedEventArgs e)
        {
            logger.Trace("Start displaying results, stopping and disposing background worker");
            _ = workers.Remove((BackgroundWorker)sender);
            ((BackgroundWorker)sender).Dispose();
            var res = (Results.MyResults)e.Result;
            logger.Trace($"There are {res.Results.Count} results");
            // If results returned select first in list.
            selectedResult = res.Results.Count > 0 ? ((Results.MyResults)e.Result).Results[0] : null;
            try
            {
                // If results older than currently displayed (earlier queries take longer) then drop results
                if (res.Timestamp > lastResultTimestamp)
                {
                    logger.Trace("Results newer so continue processing");
                    Dispatcher.Invoke(delegate
                    {
                        logger.Trace("Copying results to result collection.");
                        var results = new ObservableCollection<Results.MyResult>();
                        foreach (var item in res.Results)
                        {
                            results.Add(item);
                        }
                        SearchResults.ItemsSource = null;
                        SearchResults.ItemsSource = results;

                        if (res.Results.Count == 1)
                        {
                            logger.Trace("Single result so make selected");
                            TextBoxHost.Text = res.Results[0].Hostname;
                        }
                        //searchResults.Items.Refresh();
                    });
                    lastResultTimestamp = res.Timestamp;
                }
            }
            catch
            {
                logger.Error("Problem displaying results");
                throw;
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
                catch
                {
                    throw;
                }
                //catch (Exception ex)
                //{
                //    logger.Error("Error disposing :\n{0}", ex.Message);
                //    Trace.TraceError(ex.Message);
                //}
            }
        }

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                TextBoxHost.Text = ((Results.MyResult)((ListBox)sender).SelectedValue).Hostname;
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
                TextBoxHost.Text = ((Results.MyResult)((ListBox)sender)?.SelectedValue)?.Hostname ?? string.Empty;
            }
            catch (Exception ex)
            {
                logger.Trace(ex);
                Trace.TraceError(ex.Message);
            }
        }

        private async void Connect_Button_Click(object sender, RoutedEventArgs e)
        {
            await Connect_Button_ClickAsync(sender, e);
        }

        private async Task Connect_Button_ClickAsync(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            string directory = Path.Combine(Environment.CurrentDirectory, "tools");
            const string rcExeToRun = @"d:\rc.exe";
            const string rcW10ExeToRun = @"\\national\bbcere\BSC\Dump\Apps\sccm-remote\w10\cmrcviewer.exe";
            string vncExeToRun = Path.Combine(Path.GetTempPath(), "vncx64.exe");


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
                    startInfo.FileName = $"http://{TextBoxHost.Text.Trim()}:80/"; //DevSkim: ignore DS137138
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
                case "VMRC":
                    VCenter.VCenter.LaunchVmrc(TextBoxHost.Text.Trim(), cachedVcenter);
                    break;
                default:
                    break;
            }

            if (startInfo.FileName.Length > 0)
            {
                logger.Info("Starting: {0} with argumets {1}", startInfo.FileName, startInfo.Arguments);
                _ = await Task.Run(() => Process.Start(startInfo));

            }
            if (LvHistory.Items.Contains(TextBoxHost.Text.Trim()))
            {
                LvHistory.Items.RemoveAt(LvHistory.Items.IndexOf(TextBoxHost.Text.Trim()));
            }

            LvHistory.Items.Insert(0, TextBoxHost.Text.Trim());

            Settings.Default.history = string.Join(";", LvHistory.Items.OfType<string>().ToList());
            Settings.Default.Save();

        }



        public bool PrepareTool(byte[] resource, string outputPath)
        {
            logger.Trace("Preparing tool to path {0}", outputPath);
            if (File.Exists(outputPath))
            {
                logger.Trace("Tool path already exists.");
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
                    logger.Trace("Tool path exists and SHA256 matches, returning true");
                    return true;
                }

                logger.Warn("Tool path exists, but SHA256 doesn't match, remove file and retest");
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
                catch (IOException ex)
                {
                    logger.Error("Problem writing out tool. {0}", ex.Message);
                    _ = MessageBox.Show($"Unable to write tool to {outputPath}", "Error in preparing tool", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch
                {
                    throw;
                }
            }
            return false;
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
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Do_Search;
            worker.RunWorkerCompleted += DisplayResults;
            workers.Add(worker);
            worker.RunWorkerAsync(searchText);
        }


        private void Host_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            logger.Debug("host timer elapsed: {0}", hostText);
            hostTimer.Stop();
            BackgroundWorker connectionWorker = new BackgroundWorker();
            connectionWorker.DoWork += Do_Test_Connection;
            connectionWorker.RunWorkerCompleted += Complete_Test_Connection;
            connectionWorkers.Add(connectionWorker);
            connectionWorker.RunWorkerAsync(argument: hostText);
        }

        private void Do_Test_Connection(object sender, DoWorkEventArgs e)
        {
            logger.Info("Testing connection to {0}", e.Argument.ToString());
            Modules.ConnectionTester.TestHostConnections(e);
        }

        private void Complete_Test_Connection(object sender, RunWorkerCompletedEventArgs e)
        {
            MyConnection con = (MyConnection)e.Result;
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
                    catch (ResourceReferenceKeyNotFoundException ex)
                    {
                        logger.Error(ex);
                    }
                    catch
                    {
                        throw;
                    }

                    if (null != cachedVcenter.Value.SingleOrDefault(s => s.Name.ToLower().Trim() == ((MyConnection)e.Result).Host.ToLower().Trim())) {
                        ButtonVmrc.IsEnabled = true;
                        }
                    else
                    {
                        ButtonVmrc.IsEnabled = false;
                    }
                });
            }
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
                                catch (System.Net.Sockets.SocketException ex)
                                {
                                    Trace.TraceError(ex.Message);
                                    TextBoxHost.Text = "";
                                }
                                catch
                                {
                                    throw;
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

            Modules.PhoneBoxConfig phoneBoxConfig = Modules.PhoneBox.GetPhoneBoxConfig(((Button)sender).Content.ToString());

            if (phoneBoxConfig == null)
            {
                logger.Error("Unknonw phonebox site given");
                return;
            }

            logger.Debug("Writing config to {0}\n{1}", PhoneboxIniPath, phoneBoxConfig);

            try
            {
                File.WriteAllLines(PhoneboxIniPath, phoneBoxConfig.ToStringArray());
                logger.Info("Attempting to start Phonebox");
                try
                {
                    Process.Start(PhoneboxExePath);
                }
                catch (Win32Exception ex)
                {
                    logger.Error(ex, "Problem starting PhoneBOX");
                    App.SendReport(ex);
                }
                catch
                {
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Check file permissions for {PhoneboxIniPath}", "Problem writing configuration", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Problem writing PhoneBOX ini file - check file permission.");
                App.SendReport(ex);
            }
            catch
            {
                throw;
            }
        }

        private void TextBox_host_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TextBoxHost.IsEnabled = !TextBoxHost.IsEnabled;
        }

    }
}
