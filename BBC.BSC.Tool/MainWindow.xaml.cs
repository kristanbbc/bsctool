using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using BBC.BSC.Tool.Modules;
using BBC.BSC.Tool.Properties;
using BBC.BSC.Tool.Results;
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

        private readonly List<BackgroundWorker> _workers = new List<BackgroundWorker>();
        // ReSharper disable once CollectionNeverQueried.Local
        private readonly List<BackgroundWorker> _connectionWorkers = new List<BackgroundWorker>();
        private DateTime _lastConnectionResult = DateTime.Now;
        private DateTime _lastResultTimestamp;
        private const string LdapPath = @"LDAP://ldap.national.core.bbc.co.uk";
        private readonly Timer _searchTimer = new Timer(400);
        private readonly Timer _hostTimer = new Timer(400);
        private readonly Timer _watcher = new Timer { Interval = 1000 };
        private string _hostText;
        public readonly Logger _logger;

        private const string PhoneboxIniPath = @"C:\ProgramData\Broadcast Bionics\PhoneBOX4\client.ini";
        private const string PhoneboxExePath = @"C:\Program Files (x86)\Broadcast Bionics\PhoneBOX4\Client\PhoneBOX.Client.exe";

        private static readonly HttpClient CatHttpClient = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true });


        public MainWindow()
        {
            InitializeComponent();

            Title = "BSC Tool - Version " + Assembly.GetExecutingAssembly().GetName().Version;

            _logger = new Logging().InitLogger();

            _logger.Info("BSC Tool {0} starting.", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);

            if (Properties.Settings.Default.UpgradeRequired)
            {
                _logger.Info("Version upgrade detected - copying previous settings.");
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
            Timer watcher = _watcher;
            watcher.Elapsed += Do_Watcher;
            watcher.Enabled = true;
            ThreadPool.GetMinThreads(out int w, out int c);

            // Write the numbers of minimum threads
            _logger.Debug("Minumium number of threads available {0}, {1}", w, c);

            _ = ThreadPool.SetMinThreads(20, 10);
            _hostTimer.Elapsed += Host_Timer_Elapsed;
            _searchTimer.Elapsed += Search_Timer_Elapsed;
            DataContext = this;

            Settings.Default.history.Split(';').Where(h => h != "System.Windows.Controls.ItemCollection").ToList().ForEach(x => LvHistory.Items.Add(x));

            //If PhoneBox not installed don't enable tab
            if (!File.Exists(PhoneboxExePath))
            {
                PhoneBoxSwitcherTab.IsEnabled = false;
                UiHelper.FindVisualChildren<Button>(PhoneBoxButtons).ToList().ForEach(x => x.IsEnabled = false);
            }

            // Put Cursor in search box.
            _ = SearchIn.Focus();
            Preparer = new Preparer();
        }

        private Preparer Preparer { get; }


        /// <summary>Updates the status box if running searches are still happening.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Do_Watcher(object sender, ElapsedEventArgs e)
        {
            System.Windows.Threading.Dispatcher dispatcher = Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return;
            }

            try
            {
                dispatcher.Invoke(delegate
                {
                    if (_workers.Count > 0)
                    {
                        _logger.Debug("There are {0} workers", _workers.Count);
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
            catch (TaskCanceledException)
            {
                // The dispatcher started shutting down between the check above and the
                // Invoke call - this is expected during application close and can be ignored.
                _logger.Trace("Do_Watcher: dispatcher invoke was cancelled during shutdown.");
            }
        }

        private List<Modules.CatResult> _catResults = new List<Modules.CatResult>();


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
                _logger.Info("{1} DoSearch: {0}", e.Argument.ToString(), Environment.CurrentManagedThreadId);
                try
                {
                    string searchTerm = EscapeSqlLiteral(e.Argument.ToString().Replace("*", "%").ToLower());
                    string catQuery =
                        $"{CatPath}SELECT host_name, also_known_as, CAST(inet_ntoa(ip) as CHAR(15)) as ip, CONCAT(os,  \" \",os_version) as os FROM " +
                        "network INNER JOIN asset ON network.asset_id = asset.asset_id " +
                        "left join asset_os on asset.asset_id = asset_os.asset_id left join os on asset_os.os_id = os.os_id left join os_version on asset_os.os_version_id = os_version.os_version_id " +
                        $"WHERE life_cycle_status_id = 4 AND (lower(host_name like '%{searchTerm}%') OR IP = inet_aton('{searchTerm}') OR lower(also_known_as) LIKE '%{searchTerm}%')";

                    string jsonData;
                    _logger.Info("Running query against CAT with\n{0}", catQuery);
                    jsonData = CatHttpClient.GetStringAsync(catQuery).GetAwaiter().GetResult();

                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        _catResults = JsonConvert.DeserializeObject<List<Modules.CatResult>>(jsonData);
                        _logger.Info("Got {0} results from CAT", _catResults?.Count);

                        if (_catResults != null)
                            foreach (var item in _catResults)
                            {
                                results.Results.Add(new MyResult
                                {
                                    Source = "CAT",
                                    Hostname = item.HostName.ToUpperInvariant(),
                                    Description = item.AlsoKnownAs,
                                    OperatingSystem = item.Os,
                                    Ip = item.Ip
                                });
                            }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Error running query against CAT:\n{0}", ex.Message);
                    Trace.TraceError(ex.Message);
                }

                // Start AD search
                // Skip on Intune-managed (Azure AD/hybrid joined) devices, which typically don't
                // have a direct line to the on-prem LDAP server, avoiding a guaranteed timeout
                // delay (ClientTimeout below) on every search.
                if (!DeviceJoinDetector.IsLikelyIntuneManaged())
                {
                    try
                    {
                        int adPageSize = Settings.Default.AdPageSize > 0 ? Settings.Default.AdPageSize : 50;
                        using (DirectoryEntry dEntry = new DirectoryEntry(LdapPath))
                        using (DirectorySearcher dSearcher = new DirectorySearcher(dEntry)
                        {
                            // (|(cn=*334810*)(displayname=*334810*)(cn=PC-*334810*)(cn=B1-D0*334810*)(cn=B1-L0*334810*)(cn=61-D0*334810*)(cn=61-L0*334810*)(cn=71-D0*334810*)(cn=71-L0*334810*)(cn=91-D0*334810*)(cn=91-L0*334810*)(cn=F1-D0*334810*)(cn=F1-L0*334810*)(cn=MC-*334810*)(sn=*334810*)(samAccountName=*334810*)(mail=*334810*)(proxyaddresses=smtp:*334810*)(ou=*334810*)(&(objectcategory=printqueue)(printername=*334810*)))
                            Filter = string.Format("(&(!userAccountControl:1.2.840.113556.1.4.803:=2)(objectClass=computer)(|(cn={0}*)(displayname={0}*)(cn=PC-{0}*)(cn=B1-D0{0}*)(cn=B1-L0{0}*)(cn=31-D0{0}*)(cn=31*-D0{0}*)(cn=61-D0{0}*)(cn=61-L0{0}*)(cn=71-D0{0}*)(cn=71-L0{0}*)(cn=91-D0{0}*)(cn=91-L0{0}*)(cn=F1-D0{0}*)(cn=F1-L0{0}*)(cn=MC-{0}*)(sn={0}*)(samAccountName={0}*)))", EscapeLdapFilter(e.Argument.ToString())),
                            PageSize = adPageSize,
                            SizeLimit = adPageSize,
                            ClientTimeout = TimeSpan.FromSeconds(15)
                        })
                        {
                            dSearcher.PropertiesToLoad.Clear();
                            _ = dSearcher.PropertiesToLoad.Add("name");
                            _ = dSearcher.PropertiesToLoad.Add("description");
                            _ = dSearcher.PropertiesToLoad.Add("operatingsystem");
                            using (var sResults = dSearcher.FindAll())
                            {
                                _logger.Info("Found {0} results in Active Directory", sResults.Count);
                                foreach (SearchResult item in sResults)
                                {
                                    _logger.ConditionalTrace("AD: found: {0}", item.Properties["name"][0].ToString().ToUpperInvariant());

                                    if (results.Results.All(n => n.Hostname != item.Properties["name"][0].ToString().ToUpperInvariant()))
                                    {
                                        results.AddResult(new MyResult
                                        {
                                            Hostname = item.Properties["name"][0].ToString().ToUpperInvariant(),
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
                        _logger.Warn("Invalid Operation querying AD: ", ex);
                    }
                    catch (NotSupportedException ex)
                    {
                        _logger.Warn("LDAP query error: {0}", ex.Message);
                    }
                }
            }
            results.Results.Sort((a, b) => string.Compare(a.Hostname, b.Hostname, StringComparison.Ordinal));
            results.Results = results.Results.Distinct().ToList();
            e.Result = results;
        }

        private static string CleanResultProperty(SearchResult item,
                                                  string property) => item.Properties.Contains(property) ? item.Properties[property][0].ToString() : "";

        /// <summary>
        /// Escapes special characters in a value used inside an LDAP search filter, per RFC 4515,
        /// to prevent LDAP injection from user-supplied search input.
        /// </summary>
        private static string EscapeLdapFilter(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }

        /// <summary>
        /// Escapes single quotes in a value embedded in a SQL string literal, to prevent SQL
        /// injection from user-supplied search input sent to the CAT query endpoint.
        /// </summary>
        private static string EscapeSqlLiteral(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("'", "''");
        }

        private void Text_Changed(object sender, TextChangedEventArgs e)
        {
            _logger.ConditionalTrace("search text changed: {0}", SearchIn.Text.Trim());
            _searchText = SearchIn.Text;
            _searchTimer.Stop();
            _searchTimer.Start();
            TextBoxHost.Text = SearchIn.Text;
        }

        private MyResult _selectedResult = new MyResult();

        private void DisplayResults(object sender, RunWorkerCompletedEventArgs e)
        {
            _logger.Trace("Start displaying results, stopping and disposing background worker");
            _ = _workers.Remove((BackgroundWorker)sender);
            ((BackgroundWorker)sender).Dispose();
            var res = (MyResults)e.Result;
            _logger.Trace($"There are {res.Results.Count} results");
            // If results returned select first in list.
            _selectedResult = res.Results.Count > 0 ? ((MyResults)e.Result).Results[0] : null;
            try
            {
                // If results older than currently displayed (earlier queries take longer) then drop results
                if (res.Timestamp <= _lastResultTimestamp) return;
                _logger.Trace("Results newer so continue processing");
                Dispatcher.Invoke(delegate
                {
                    _logger.Trace("Copying results to result collection.");
                    var results = new ObservableCollection<MyResult>();
                    foreach (var item in res.Results)
                    {
                        results.Add(item);
                    }
                    SearchResults.ItemsSource = null;
                    SearchResults.ItemsSource = results;

                    if (res.Results.Count != 1) return;
                    _logger.Trace("Single result so make selected");
                    TextBoxHost.Text = res.Results[0].Hostname;
                });
                _lastResultTimestamp = res.Timestamp;
            }
            catch
            {
                _logger.Error("Problem displaying results");
                throw;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Settings.Default.Save();
            if (_workers.Count > 0)
            {
                _logger.Info("Unable to close as workers still running");
                e.Cancel = true;
            }
            else
            {
                // Stop all recurring timers before the window/dispatcher shuts down, otherwise
                // a timer tick can race with dispatcher shutdown and throw TaskCanceledException
                // from Dispatcher.Invoke (e.g. Do_Watcher).
                _watcher.Stop();
                _searchTimer.Stop();
                _hostTimer.Stop();

                foreach (BackgroundWorker item in _workers)
                {
                    item.Dispose();
                }
                //latestRestults.Dispose();
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
                TextBoxHost.Text = ((MyResult)((ListBox)sender)?.SelectedValue)?.Hostname ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);

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
                _logger.Trace(ex);
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
                    // Note: runas.exe depends on the Secondary Logon service, which is commonly
                    // disabled on Intune-managed (Azure AD/hybrid joined) devices, causing the
                    // connection to silently fail. On those devices we launch mstsc.exe directly
                    // with /prompt so the user can enter the national\<ere> account in RDP's own
                    // credential dialog instead, without needing Secondary Logon.
                    DeviceJoinType joinType = DeviceJoinDetector.GetJoinType();
                    _logger.Info("Device join type: {0}", joinType);

                    if (DeviceJoinDetector.IsLikelyIntuneManaged())
                    {
                        startInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe");
                        startInfo.Arguments = $"/v:{TextBoxHost.Text.Trim()} /prompt";
                        startInfo.UseShellExecute = true;
                    }
                    else
                    {
                        startInfo.FileName = "cmd";
                        startInfo.Arguments =
                            $"/c runas /user:national\\{TextBoxEre.Text} \"mstsc.exe /v:{TextBoxHost.Text} /prompt\"";
                    }

                    break;

                case "RC7":
                    if (Preparer.PrepareTool(Properties.Resources.rc, rcExeToRun))
                    {
                        startInfo.Arguments =
                            $"/c runas /user:national\\{TextBoxEre.Text} \"{rcExeToRun} 1 {TextBoxHost.Text.Trim()}\"";
                        startInfo.FileName = "cmd";
                    }
                    break;
                case "RC10":

                    startInfo.Arguments =
                        $"/c runas /user:national\\{TextBoxEre.Text} \"{rcW10ExeToRun} {TextBoxHost.Text.Trim()}\"";
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
                    if (Preparer.PrepareTool(Properties.Resources.vncx64, vncExeToRun))
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
                    foreach (var item in logViewPaths)
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
                _logger.Info("Starting: {0} with arguments {1}", startInfo.FileName, startInfo.Arguments);
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


        private void TextBox_host_TextChanged(object sender, TextChangedEventArgs e)
        {
            _logger.ConditionalTrace("host text changed: {0}", TextBoxHost.Text.Trim());
            _hostText = TextBoxHost.Text.Trim();
            _hostTimer.Stop();
            _hostTimer.Start();
        }

        private string _searchText;

        private void Search_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _logger.Trace("search timer elapsed: {0}", _searchText);
            _searchTimer.Stop();
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Do_Search;
            worker.RunWorkerCompleted += DisplayResults;
            _workers.Add(worker);
            worker.RunWorkerAsync(_searchText);
        }


        private void Host_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _logger.Debug("host timer elapsed: {0}", _hostText);
            _hostTimer.Stop();
            BackgroundWorker connectionWorker = new BackgroundWorker();
            connectionWorker.DoWork += Do_Test_Connection;
            connectionWorker.RunWorkerCompleted += Complete_Test_Connection;
            _connectionWorkers.Add(connectionWorker);
            connectionWorker.RunWorkerAsync(argument: _hostText);
        }

        private void Do_Test_Connection(object sender, DoWorkEventArgs e)
        {
            _logger.Info("Testing connection to {0}", e.Argument.ToString());
            Modules.ConnectionTester.TestHostConnections(e);
        }

        private void Complete_Test_Connection(object sender, RunWorkerCompletedEventArgs e)
        {
            MyConnection con = (MyConnection)e.Result;
            _logger.Trace("Conection test to {1} completed at {0}", con.Timestamp, con.Host);
            if (_lastConnectionResult < con.Timestamp)
            {
                Dispatcher.Invoke(delegate
                {
                    _logger.Trace("setting all buttons as per results");
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
                    _lastConnectionResult = con.Timestamp;

                    ButtonRcW10.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    ButtonRc.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    ButtonRdp.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    try
                    {
                        if (_selectedResult?.OperatingSystem == null) return;
                        if (_selectedResult.OperatingSystem.Contains("Windows 10"))
                        {
                            ButtonRcW10.Style = (Style)FindResource("MaterialDesignRaisedAccentButton");
                        }
                        else if (_selectedResult.OperatingSystem.Contains("Windows 7"))
                        {
                            ButtonRc.Style = (Style)FindResource("MaterialDesignRaisedAccentButton");
                        }
                        else if (_selectedResult.OperatingSystem.Contains("Windows Server"))
                        {
                            ButtonRdp.Style = (Style)FindResource("MaterialDesignRaisedAccentButton");
                        }
                    }
                    catch (ResourceReferenceKeyNotFoundException ex)
                    {
                        _logger.Error(ex);
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
            _logger.Info("Phonebox button pressed with content {0}", ((Button)sender).Content);

            if (!File.Exists(PhoneboxIniPath))
            {
                _logger.Error("Phonebox ini file doesn't exist at {0}. Not switching.", PhoneboxIniPath);
                return;
            }
            if (Process.GetProcessesByName("PhoneBOX.Client").Count() != 0)
            {
                _logger.Warn("Phonebox running, will not continue.");
                MessageBox.Show(messageBoxText: "Close PhoneBOX before continuing.", caption: "ERROR", button: MessageBoxButton.OK, icon: MessageBoxImage.Warning);
                return;
            }

            Modules.PhoneBoxConfig phoneBoxConfig = Modules.PhoneBox.GetPhoneBoxConfig(((Button)sender).Content.ToString());

            if (phoneBoxConfig == null)
            {
                _logger.Error("Unknonw phonebox site given");
                return;
            }

            _logger.Debug("Writing config to {0}\n{1}", PhoneboxIniPath, phoneBoxConfig);

            try
            {
                File.WriteAllLines(PhoneboxIniPath, phoneBoxConfig.ToStringArray());
                _logger.Info("Attempting to start Phonebox");
                try
                {
                    Process.Start(PhoneboxExePath);
                }
                catch (Win32Exception ex)
                {
                    _logger.Error(ex, "Problem starting PhoneBOX");
                    App.SendReport(ex);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Check file permissions for {PhoneboxIniPath}", "Problem writing configuration", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.Error(ex, "Problem writing PhoneBOX ini file - check file permission.");
                App.SendReport(ex);
            }
        }

        private void TextBox_host_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TextBoxHost.IsEnabled = !TextBoxHost.IsEnabled;
        }

    }
}
