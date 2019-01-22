using System;
using System.Collections.Generic;
using System.Linq;
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
using System.DirectoryServices;
using System.ComponentModel;
using System.Runtime.Caching;
using System.Timers;
using System.Diagnostics;
using System.Net.Sockets;
using MySql;
using MySql.Data.MySqlClient;

namespace BBC.BSC.Tool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {



     
        SearchResultCollection latestRestults;
        private List<BackgroundWorker> workers = new List<BackgroundWorker>();
        private List<BackgroundWorker> connectionWorkers = new List<BackgroundWorker>();
        private DateTime LastConnectionResult;
        private DateTime lastResultTimestamp;


        public MainWindow()
        {
            InitializeComponent();
            System.Timers.Timer watcher = new System.Timers.Timer
            {
                Interval = 1000
            };
            watcher.Elapsed += Do_Watcher;
            watcher.Enabled = true;
           

        }

      
        private void Do_Watcher(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine(workers.Count);

            Dispatcher.Invoke((Action)delegate () {

             if (workers.Count > 0)
            {
                    this.status.Fill = new SolidColorBrush(Colors.Red);
               // this.Title = "Busy";
            }
            else
            {
                    this.status.Fill = new SolidColorBrush(Colors.Green);
                //this.Title = "Finished";
            }

            });

      


            
        }

        void DoSearch(object sender, DoWorkEventArgs e)
        {
            DateTime timestamp = DateTime.Now;

            Console.WriteLine("DoSearch: {0}", e.Argument.ToString());
            List<string> output = new List<string>();
            if (e.Argument.ToString().Length < 3)
            {
                e.Result = null;
            }
            else
            {

                using (var conn = new MySqlConnection())
                    {
                try
                {
                        conn.ConnectionString = "server=bbcws3001;port=3306;uid=mms1;pwd=System1;database=asset;SslMode=none";
                        conn.Open();
                        MySqlCommand cmd = new MySqlCommand(string.Format("SELECT host_name, also_known_as  FROM network INNER JOIN asset ON network.asset_id = asset.asset_id WHERE life_cycle_status_id = 4 AND host_name like '%{0}%'", e.Argument.ToString()), conn);
                        MySqlDataReader rdr = cmd.ExecuteReader();

                        while (rdr.Read())
                        {
                            //Console.WriteLine(rdr[0] + " -- " + rdr[1]);
                            output.Add(rdr[0].ToString().ToUpper());
                        }
                        rdr.Close();
                 }
                 
                catch (Exception ex)
                {
                         Console.WriteLine(ex.Message);
                }  
                        conn.Close();
                }


                try
                {
                    string path = @"LDAP://national";


                    using (DirectoryEntry dEntry = new DirectoryEntry(path))
                    using (DirectorySearcher dSearcher = new DirectorySearcher(dEntry)
                    {
                        // (|(cn=*334810*)(displayname=*334810*)(cn=PC-*334810*)(cn=B1-D0*334810*)(cn=B1-L0*334810*)(cn=61-D0*334810*)(cn=61-L0*334810*)(cn=71-D0*334810*)(cn=71-L0*334810*)(cn=91-D0*334810*)(cn=91-L0*334810*)(cn=F1-D0*334810*)(cn=F1-L0*334810*)(cn=MC-*334810*)(sn=*334810*)(samAccountName=*334810*)(mail=*334810*)(proxyaddresses=smtp:*334810*)(ou=*334810*)(&(objectcategory=printqueue)(printername=*334810*)))
                        //Filter = string.Format("(&(objectClass=computer)(cn={0}*))", e.Argument.ToString()),
                        Filter = string.Format("(&(!userAccountControl:1.2.840.113556.1.4.803:=2)(objectClass=computer)(|(cn={0}*)(displayname={0}*)(cn=PC-{0}*)(cn=B1-D0{0}*)(cn=B1-L0{0}*)(cn=31-D0{0}*)(cn=31*-D0{0}*)(cn=61-D0{0}*)(cn=61-L0{0}*)(cn=71-D0{0}*)(cn=71-L0{0}*)(cn=91-D0{0}*)(cn=91-L0{0}*)(cn=F1-D0{0}*)(cn=F1-L0{0}*)(cn=MC-{0}*)(sn={0}*)(samAccountName={0}*)(mail={0}*)(proxyaddresses=smtp:{0}*)(ou={0}*)(&(objectcategory=printqueue)(printername={0}*))))", e.Argument.ToString()),
                        PageSize = 20,
                        ServerTimeLimit = new TimeSpan(2000),
                        ServerPageTimeLimit = new TimeSpan(3000),
                        SizeLimit = 20
                        
                    })
                    {
                        using (SearchResultCollection sResults = dSearcher.FindAll())
                        {
                            latestRestults = sResults;
                            foreach (SearchResult item in sResults)
                            {
                                //ResultPropertyCollection fields = item.Properties;
                                ResultPropertyValueCollection name = item.Properties["name"];
                                output.Add(name[0].ToString().ToUpper());
                            }
                        }
                       
                    }
                }
                catch (Exception)
                {

                    //throw;
                }

            }
            //e.Result = output.ToArray();

            output.Sort();


            MyResults res = new MyResults
            {
                results = output.Distinct().ToArray(),
                timestamp = timestamp
            };
            e.Result = res;
       
        }
        
        private void DoSearch(object sender, TextChangedEventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += DoSearch;
            worker.RunWorkerCompleted += DisplayResults;
            workers.Add(worker);
            worker.RunWorkerAsync(((TextBox)sender).Text);
        }

        private void DisplayResults(object sender, RunWorkerCompletedEventArgs e)
        {
            workers.Remove((BackgroundWorker)sender);
            ((BackgroundWorker)sender).Dispose();
            //listBox.Items.Clear();

            MyResults res = (MyResults)e.Result;
            try
            {
                if (res.timestamp > lastResultTimestamp)
                {

                listBox.ItemsSource = (String[])res.results;
                    lastResultTimestamp = res.timestamp;
                }

            }
            catch (Exception)
            {
                
            }// listBox.ItemsSource = (String[])e.Result;

            //foreach (var item in (String[])e.Result)
            //{
               //    listBox.Items.Add(item);
            //}

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Save();

            try
            {
            latestRestults.Dispose();
                        
            }
            catch (Exception)
            {

               
            }
            foreach (var item in workers)
                        {
                            item.Dispose();
                        }

        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            textbox_host.Text = ((ListBox)sender).SelectedValue.ToString();
        }

        private void ListBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
            textbox_host.Text = ((ListBox)sender).SelectedValue.ToString();

            }
            catch
            {

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
                    string RcArguments = String.Format(@"/c runas /user:national\{0} /savecred ""{1} 1 {2}""", textBox_ere.Text, RcFileName, textbox_host.Text.Trim());
                    Console.WriteLine(RcArguments);
                    startInfo.Arguments = RcArguments;
                    startInfo.FileName = "cmd";
                    startInfo.WorkingDirectory = directory;
                    break;


                case "button_SSH":
                    startInfo.Arguments = String.Format("{0}", textbox_host.Text);
                    startInfo.FileName = System.IO.Path.Combine(directory, "putty.exe");
                    break;

                case "button_SSH_ERE":

               
                    startInfo.Arguments = String.Format("{1}@{0}", textbox_host.Text, textBox_ere.Text.Trim());
                    startInfo.FileName = System.IO.Path.Combine(directory, "putty.exe");
                    break;

                case "button_TELNET":
                    startInfo.Arguments = String.Format("-telnet -P 23 {0}", textbox_host.Text);
                    startInfo.FileName = System.IO.Path.Combine(directory, "putty.exe");
                    break;

                case "button_VNC":

                    byte[] VncExeBytes = Properties.Resources.vncx64;
                    string VncExeToRun = @"d:\vncx64.exe";
                    using (System.IO.FileStream exeFile = new System.IO.FileStream(VncExeToRun, System.IO.FileMode.Create))
                    {
                        exeFile.Write(VncExeBytes, 0, VncExeBytes.Length);
                    }

                    startInfo.Arguments = String.Format(@"{1} username={0} ", textBox_ere.Text,  textbox_host.Text.Trim());
                    startInfo.FileName = VncExeToRun;
                    startInfo.WorkingDirectory = directory;
                    break;

                case "button_HTTP":
                    startInfo.FileName = String.Format("http://{0}:80/", textbox_host.Text.Trim());
                    break;
                case "button_HTTPS":
                    startInfo.FileName = String.Format("https://{0}:443/", textbox_host.Text.Trim());
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

            BackgroundWorker connectionWorker = new BackgroundWorker();
            connectionWorker.DoWork += Do_Test_Connection;
            connectionWorker.RunWorkerCompleted += Complete_Test_Connection;
            connectionWorkers.Add(connectionWorker);
            connectionWorker.RunWorkerAsync(argument: textbox_host.Text.Trim());


        }
        

        private void Complete_Test_Connection(object sender, RunWorkerCompletedEventArgs e)
        {
            MyConnection con = (MyConnection)e.Result;
            if (LastConnectionResult < con.timestamp)
            {
                this.button_RDP.IsEnabled = con.rdp;
                this.button_RC.IsEnabled = con.rdp;
                this.button_VNC.IsEnabled = con.vnc;
                this.button_SSH.IsEnabled = con.ssh;
                this.button_SSH_ERE.IsEnabled = con.ssh;
                this.button_HTTP.IsEnabled = con.http;
                this.button_HTTPS.IsEnabled = con.https;
                this.button_TELNET.IsEnabled = con.telnet;
                LastConnectionResult = con.timestamp;
            }



        }

        private void Do_Test_Connection(object sender, DoWorkEventArgs e)
        {
            using (MyConnection con = new MyConnection())
            {
                int timeout = 200;

                if (IsPortOpen(e.Argument.ToString(), 3389, TimeSpan.FromMilliseconds(timeout)))
                    con.rdp = true;
                if (IsPortOpen(e.Argument.ToString(), 5900, TimeSpan.FromMilliseconds(timeout)))
                    con.vnc = true;
                if (IsPortOpen(e.Argument.ToString(), 22, TimeSpan.FromMilliseconds(timeout)))
                    con.ssh = true;
                if (IsPortOpen(e.Argument.ToString(), 23, TimeSpan.FromMilliseconds(timeout)))
                    con.telnet = true;
                if (IsPortOpen(e.Argument.ToString(), 80, TimeSpan.FromMilliseconds(timeout)))
                    con.http = true;
                if (IsPortOpen(e.Argument.ToString(), 443, TimeSpan.FromMilliseconds(timeout)))
                    con.https = true;
                
                e.Result = con; 
            }


        }
        
        bool IsPortOpen(string host, int port, TimeSpan timeout)
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
        

    }
    class MyResults
    {

        public DateTime timestamp;
        public object results;

    }

    class MyConnection : IDisposable
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
            this.Source = Properties.Settings.Default;
            this.Mode = BindingMode.TwoWay;
        }
    }
}
