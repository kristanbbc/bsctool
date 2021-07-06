using System;
using System.Collections.Generic;

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
        }

        public override string ToString()
        {
            string output = $"Connection status of {Host} as of {Timestamp}:" + Environment.NewLine;
            output += $"RDP:      {Rdp}" + Environment.NewLine;
            output += $"VNC:      {Vnc}" + Environment.NewLine;
            output += $"SSH:      {Ssh}" + Environment.NewLine;
            output += $"HTTP:     {Http}" + Environment.NewLine;
            output += $"HTTPS:    {Https}" + Environment.NewLine;
            output += $"Telnet:   {Telnet}" + Environment.NewLine;
            output += $"Dira Log: {DiraLogView}";

            return output;
        }

        public Dictionary<string, bool> Status()
        {
            return new Dictionary<string, bool>()
            {
                { "RDP", Rdp },
                { "VNC", Vnc },
                { "SSH", Ssh },
                { "HTTP", Http },
                { "HTTPS", Https },
                { "Telnet", Telnet },
                { "DiraLog", DiraLogView },
            };
        }
    }

}
