using System;
using System.ComponentModel;
using System.Net.Sockets;

namespace BBC.BSC.Tool.Modules
{
    public class ConnectionTester
    {
        public static void TestHostConnections( DoWorkEventArgs e)
        {
            using (var con = new MyConnection())
            {
                con.Host = e.Argument.ToString();
                // If short don't test
                if (e.Argument.ToString().Length < 4)
                {
                    e.Result = con;
                    return;
                }
                // TODO: make user config
                TimeSpan timeout = TimeSpan.FromMilliseconds(200);

                con.Rdp = IsPortOpen(e.Argument.ToString(), 3389, timeout);
                con.Vnc = IsPortOpen(e.Argument.ToString(), 5900, timeout);
                con.Ssh = IsPortOpen(e.Argument.ToString(), 22, timeout);
                con.Telnet = IsPortOpen(e.Argument.ToString(), 23, timeout);
                con.Http = IsPortOpen(e.Argument.ToString(), 80, timeout);
                con.Https = IsPortOpen(e.Argument.ToString(), 443, timeout);
                con.DiraLogView = IsPortOpen(e.Argument.ToString(), 5100, timeout);


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
    }
}
