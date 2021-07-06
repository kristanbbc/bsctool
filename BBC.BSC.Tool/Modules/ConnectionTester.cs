using NLog;
using System;
using System.ComponentModel;
using System.Net.Sockets;

namespace BBC.BSC.Tool.Modules
{
    public class ConnectionTester
    {
        // private static NLog.Logger logger;

        private ConnectionTester()
        {
            Logger logger = new Logging().initLogger();
        }

        /// <summary>
        /// Tests the TCP port connections to a given host - triggerd from a dowork.
        /// </summary>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        public static void TestHostConnections(DoWorkEventArgs e)
        {
            Logger logger = new Logging().initLogger();
            logger.ConditionalTrace("Testing TCP connections to {0}", e.Argument.ToString());
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


                logger.ConditionalTrace("Connection results{0}", con.ToString());
                e.Result = con;
            }
        }

        /// <summary>
        /// Determines whether [is port open] [the specified host].
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="port">The port.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns>
        ///   <c>true</c> if [is port open] [the specified host]; otherwise, <c>false</c>.
        /// </returns>
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
