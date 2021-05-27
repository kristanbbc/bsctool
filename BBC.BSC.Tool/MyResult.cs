namespace BBC.BSC.Tool
{
    public class MyResult
    {
        public string Source { get; set; }
        public string Hostname { get; set; }
        public string Ip { get; set; }
        public string Description { get; set; }
        public string OperatingSystem { get; set; }
        public MyResult()
        {

        }
        public MyResult(string hostname, string source = null)
        {
            Hostname = hostname;
            Source = source;
        }
        public MyResult(string hostname, string ip, string source = null)
        {
            Hostname = hostname;
            Ip = ip;
            Source = source;
        }

    }
}