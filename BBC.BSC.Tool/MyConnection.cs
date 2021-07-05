using System;

namespace BBC.BSC.Tool
{
    internal class MyConnection : IDisposable
    {
        public DateTime Timestamp = DateTime.Now;
        public string Host;
        public bool Rdp;
        public bool Vnc;
        public bool Ssh;
        public bool Http;
        public bool Https;
        public bool Telnet;
        public bool DiraLogView;

        public void Dispose()
        {
            //    this.rdp = null;
            //    this.vnc = null;
            //    this.ssh = null;
            //    this.http = null;
            //    this.telnet = null;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}