namespace BBC.BSC.Tool
{
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
            return new[] {
                "[server]",
                $"backupaddress = {ServerBackupAddress}",
                $"address = {ServerAddress}",
                "[oasis]",
                $"address = {OasisAddress}",
                $"backupaddress = {OasisBackupAddress}"
            };

        }
    }
}