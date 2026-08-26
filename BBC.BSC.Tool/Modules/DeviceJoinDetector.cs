using System;
using System.Diagnostics;
using NLog;

namespace BBC.BSC.Tool.Modules
{
    public enum DeviceJoinType
    {
        Unknown,
        DomainJoined,      // Classic on-prem AD / SCCM managed
        AzureAdJoined,     // Intune managed, cloud-only
        HybridAzureAdJoined, // Both AD and Azure AD joined
        WorkplaceJoined
    }

    /// <summary>
    /// Detects whether the current machine is domain-joined (typically SCCM managed)
    /// or Azure AD / hybrid joined (typically Intune managed), by parsing the output
    /// of the built-in "dsregcmd /status" tool.
    /// </summary>
    internal static class DeviceJoinDetector
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static DeviceJoinType? _cached;

        public static DeviceJoinType GetJoinType()
        {
            if (_cached.HasValue)
            {
                return _cached.Value;
            }

            bool domainJoined = false;
            bool azureAdJoined = false;

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "dsregcmd.exe",
                    Arguments = "/status",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);

                    domainJoined = ContainsYes(output, "DomainJoined");
                    azureAdJoined = ContainsYes(output, "AzureAdJoined");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Unable to determine device join type via dsregcmd.");
            }

            DeviceJoinType result;
            if (domainJoined && azureAdJoined)
            {
                result = DeviceJoinType.HybridAzureAdJoined;
            }
            else if (azureAdJoined)
            {
                result = DeviceJoinType.AzureAdJoined;
            }
            else if (domainJoined)
            {
                result = DeviceJoinType.DomainJoined;
            }
            else
            {
                result = DeviceJoinType.Unknown;
            }

            _cached = result;
            Logger.Info("Detected device join type: {0}", result);
            return result;
        }

        /// <summary>
        /// True when the device is Intune-managed (Azure AD joined or hybrid joined),
        /// meaning tools relying on classic AD-only features (e.g. Secondary Logon/runas
        /// against an on-prem domain account) may not be reliable.
        /// </summary>
        public static bool IsLikelyIntuneManaged()
        {
            DeviceJoinType type = GetJoinType();
            return type == DeviceJoinType.AzureAdJoined || type == DeviceJoinType.HybridAzureAdJoined;
        }

        private static bool ContainsYes(string output, string key)
        {
            int index = output.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            int colonIndex = output.IndexOf(':', index);
            if (colonIndex < 0)
            {
                return false;
            }

            int lineEnd = output.IndexOf('\n', colonIndex);
            string valuePart = lineEnd > 0
                ? output.Substring(colonIndex + 1, lineEnd - colonIndex - 1)
                : output.Substring(colonIndex + 1);

            return valuePart.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase);
        }
    }
}
