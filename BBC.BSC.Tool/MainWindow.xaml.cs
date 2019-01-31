using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Net.Sockets;
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
        private SearchResultCollection latestRestults;
        private List<BackgroundWorker> workers = new List<BackgroundWorker>();
        private List<BackgroundWorker> connectionWorkers = new List<BackgroundWorker>();
        private DateTime LastConnectionResult = DateTime.Now;
        private DateTime lastResultTimestamp;
        private readonly string path = @"LDAP://ldap.national.core.bbc.co.uk";
        private Timer searchTimer = new Timer(400);
        private Timer hostTimer = new Timer(400);
        private MySqlConnection conn = new MySqlConnection("server=bbcws3001;port=3306;uid=catread;pwd=reader;database=asset;SslMode=none");
        private string host_text;


        public MainWindow()
        {
            InitializeComponent();
            System.Timers.Timer watcher = new System.Timers.Timer
            {
                Interval = 1000
            };
            watcher.Elapsed += Do_Watcher;
            watcher.Enabled = true;
            System.Threading.ThreadPool.GetMinThreads(out int w, out int c);

            // Write the numbers of minimum threads
            Console.WriteLine("{0}, {1}",
                w,
                c);

            System.Threading.ThreadPool.SetMinThreads(20, 10);
            hostTimer.Elapsed += Host_Timer_Elapsed;
            searchTimer.Elapsed += Search_Timer_Elapsed;
            DataContext = this;
            searchResults.ItemsSource = Results;
        }


        /// <summary>Updates the status box if running searches are still happening.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Do_Watcher(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine(workers.Count);
            Dispatcher.Invoke(delegate ()
            {
                if (workers.Count > 0)
                {
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
                Console.WriteLine("{1} DoSearch: {0}", e.Argument.ToString(), Environment.CurrentManagedThreadId);
                try
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                    {
                        conn.Open();
                    }
                    MySqlCommand cmd = new MySqlCommand(string.Format("SELECT host_name, also_known_as, CAST(inet_ntoa(ip) as CHAR(15)) as ip FROM network INNER JOIN asset ON network.asset_id = asset.asset_id WHERE life_cycle_status_id = 4 AND (host_name like '%{0}%' OR IP = inet_aton('{0}') OR also_known_as LIKE '%{0}%')", e.Argument.ToString().Replace("*", "%")), conn);
                    MySqlDataReader rdr = cmd.ExecuteReader();

                    while (rdr.Read())
                    {
                        //Console.WriteLine(rdr[0] + " -- " + rdr[1]);
                        results.results.Add(new MyResult
                        {
                            Source = "CAT",
                            Hostname = rdr["host_name"].ToString().ToUpper(),
                            Description = rdr["also_known_as"].ToString(),
                            Ip = rdr["ip"].ToString().Trim()
                        });
                    }
                    rdr.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Trace.TraceError(ex.Message);
                }
                try
                {
                    using (DirectoryEntry dEntry = new DirectoryEntry(path))
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
                            foreach (SearchResult item in sResults)
                            {
                                //Console.WriteLine("AD: found: {0}", item.Properties["name"][0].ToString().ToUpper());
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
                    Console.WriteLine("LDAP error: {0}", ex.Message);
                }
            }
            results.results.Sort((a, b) => a.Hostname.CompareTo(b.Hostname));
            e.Result = results;
        }

        private void Text_Changed(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine("search text changed: {0}", searchIn.Text.Trim());
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
                        //searchResults.Items.Refresh();
                    });
                    lastResultTimestamp = res.timestamp;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                Console.WriteLine(ex.Message);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Save();


            if (workers.Count > 0)
            {
                e.Cancel = true;
                return;
            }
            else
            {
                try
                {
                    latestRestults.Dispose();
                    conn.Close();
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message);
                }
                foreach (BackgroundWorker item in workers)
                {
                    item.Dispose();
                }
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
                Trace.TraceError(ex.Message);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
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
                    byte[] exeBytes = Properties.Resources.rc;
                    string exeToRun = @"d:\rc.exe";
                    using (System.IO.FileStream exeFile = new System.IO.FileStream(exeToRun, System.IO.FileMode.Create))
                    {
                        exeFile.Write(exeBytes, 0, exeBytes.Length);
                    }
                    string RcFileName = System.IO.Path.Combine(directory, "rc.exe");
                    RcFileName = exeToRun;
                    string RcArguments = string.Format(@"/c runas /user:national\{0} /savecred ""{1} 1 {2}""", textBox_ere.Text, RcFileName, textbox_host.Text.Trim());
                    Console.WriteLine(RcArguments);
                    startInfo.Arguments = RcArguments;
                    startInfo.FileName = "cmd";
                    startInfo.WorkingDirectory = directory;
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
                    byte[] VncExeBytes = Properties.Resources.vncx64;
                    string VncExeToRun = @"d:\vncx64.exe";
                    using (System.IO.FileStream exeFile = new System.IO.FileStream(VncExeToRun, System.IO.FileMode.Create))
                    {
                        exeFile.Write(VncExeBytes, 0, VncExeBytes.Length);
                    }
                    startInfo.Arguments = string.Format(@"/c runas /user:national\{0} /savecred ""{2} {1}""", textBox_ere.Text, textbox_host.Text.Trim(), VncExeToRun);
                    startInfo.FileName = "cmd";
                    startInfo.WorkingDirectory = directory;
                    break;
                case "button_HTTP":
                    startInfo.FileName = string.Format("http://{0}:80/", textbox_host.Text.Trim());
                    break;
                case "button_HTTPS":
                    startInfo.FileName = string.Format("https://{0}:443/", textbox_host.Text.Trim());
                    break;
                default:
                    break;
            }
            Process.Start(startInfo);
        }

        private void TextBox_ere_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.ere = ((TextBox)sender).Text;
            Properties.Settings.Default.Save();
        }


        private void Textbox_host_TextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine("host text changed: {0}", textbox_host.Text.Trim());
            host_text = textbox_host.Text.Trim();
            hostTimer.Stop();
            hostTimer.Start();
        }

        private string search_text;

        private void Search_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("search timer elapsed: {0}", search_text);
            searchTimer.Stop();
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Do_Search;
            worker.RunWorkerCompleted += DisplayResults;
            workers.Add(worker);
            worker.RunWorkerAsync(search_text);
        }


        private void Host_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("host timer elapsed: {0}", host_text);
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
                    LastConnectionResult = con.timestamp;
                });
            }



        }

        private void Do_Test_Connection(object sender, DoWorkEventArgs e)
        {

            Console.WriteLine("Testing connection to {0}", e.Argument.ToString());
            using (MyConnection con = new MyConnection())
            {
                if (e.Argument.ToString().Length < 4)
                {
                    e.Result = con;
                    return;
                }
                int timeout = 200;

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

        private void Button_Click_1(object sender, RoutedEventArgs e)
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
}
