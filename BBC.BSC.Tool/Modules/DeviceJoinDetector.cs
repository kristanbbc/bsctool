using System;
using System.Diagnostics;
using Microsoft.Win32;
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
    /// of the built-in "dsregcmd /status" tool, with a registry-based fallback.
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

            bool? domainJoined = null;
            bool? azureAdJoined = null;

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
                    bool exited = process.WaitForExit(5000);

                    Logger.Debug("dsregcmd exited: {0}, ExitCode: {1}", exited, exited ? process.ExitCode : (int?)null);
                    Logger.Trace("dsregcmd /status raw output:\n{0}", output);

                    if (exited && !string.IsNullOrWhiteSpace(output))
                    {
                        domainJoined = ContainsYes(output, "DomainJoined");
                        azureAdJoined = ContainsYes(output, "AzureAdJoined");
                    }
                    else
                    {
                        Logger.Warn("dsregcmd did not exit in time or produced no output; falling back to registry check.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Unable to determine device join type via dsregcmd, falling back to registry check.");
            }

            // Fallback: check for the Azure AD join registry marker directly, in case dsregcmd
            // is unavailable, blocked, or its output format couldn't be parsed.
            if (azureAdJoined == null)
            {
                azureAdJoined = IsAzureAdJoinedViaRegistry();
            }

            DeviceJoinType result;
            if (domainJoined == true && azureAdJoined == true)
            {
                result = DeviceJoinType.HybridAzureAdJoined;
            }
            else if (azureAdJoined == true)
            {
                result = DeviceJoinType.AzureAdJoined;
            }
            else if (domainJoined == true)
            {
                result = DeviceJoinType.DomainJoined;
            }
            else
            {
                result = DeviceJoinType.Unknown;
            }

            _cached = result;
            Logger.Info("Detected device join type: {0} (domainJoined={1}, azureAdJoined={2})", result, domainJoined, azureAdJoined);
            return result;
        }

        /// <summary>
        /// True when the device is likely Intune-managed (Azure AD joined, hybrid joined, or
        /// undetermined), meaning tools relying on classic AD-only features (e.g. Secondary
        /// Logon/runas against an on-prem domain account) may not be reliable. When detection
        /// is inconclusive we default to true, since the mstsc /prompt fallback works fine on
        /// domain-joined machines too, whereas runas silently failing on an Intune machine is
        /// the more disruptive failure mode.
        /// </summary>
        public static bool IsLikelyIntuneManaged()
        {
            DeviceJoinType type = GetJoinType();
            return type != DeviceJoinType.DomainJoined;
        }

        private static bool IsAzureAdJoinedViaRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo"))
                {
                    bool found = key?.GetSubKeyNames().Length > 0;
                    Logger.Debug("Registry fallback AzureAdJoined check: {0}", found);
                    return found;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Unable to determine Azure AD join state via registry fallback.");
                return false;
            }
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
