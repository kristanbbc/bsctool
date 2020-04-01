using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BBC.BSC.Tool.Server
{
    [Serializable]
    public class Item
    {
        private string _Hostname;

        public string Hostname
        {
            get { return _Hostname; }
            set
            {
                _Hostname = value.ToUpper().Trim();
                this.nons = new Nons(_Hostname);
            }
        }


        public string IPAddress;
        public string Description;
        public string Source;
        public string OperatingSystem;
        public Nons nons;

        public string Id
        {
            get { return Hostname; }
        }
        public DateTime timestamp
        {
            get { return DateTime.Now; }
        }

    }
    [Serializable]
    public class Nons
    {

        public override string ToString()
        {


            string output = $"Business: {this.Business} \n";
            output += $"CountryCode: {this.CountryCode} \n";
            output += $"LocationCode: {this.LocationCode} \n";
            output += $"FunctionCode: {this.FunctionCode} \n";
            output += $"SubFunctionCode: {this.SubFunctionCode} \n";
            output += $"RoleCode: {this.RoleCode} \n";
            output += $"ServerNumber: {this.ServerNumber} \n";
            output += $"Platform: {this.Platform} \n";
            output += $"DeviceType: {this.DeviceType} \n";
            output += $"Instance: {this.Instance} \n";
            output += $"Owner: {this.Owner} \n";
            output += $"AssetNumber: {this.AssetNumber} \n";
            output += $"AdditionalText: {this.AdditionalText} \n";


            return output;
            //return string.Join("\n", GetAllPropertyValues());

        }

        public List<string> GetAllPropertyValues()
        {
            List<string> values = new List<string>();
            foreach (var pi in typeof(string).GetProperties())
            {
                values.Add(string.Format("{0} : {1}", pi.Name, pi.GetValue(this, null).ToString()));
            }

            return values;
        }


        public Nons(string hostname)
        {
            Dictionary<string, Regex> regices = new Dictionary<string, Regex>();
            regices.Add("nons2008", new Regex(@"^(\d)(GB|GG|JE)([A-Z0-9]{2})([\w\d]{2})(\w{2,3})?([B|\d])([A-Z0-9]{3})(.*)?"));
            regices.Add("workstation", new Regex(@"^([\d\w])(\d)(\w{2})?-(D|L|V)(\d)(\w)(\d{6})(.*)"));
            regices.Add("workstation-xp-mac", new Regex(@"^(PC|MC)-(\w)(\d{6})(.*)"));
            regices.Add("bncs", new Regex(@"^(ERE)([\w\d]{2,3})(BNCS)(\d{2,3})$"));
            regices.Add("linuxnons", new Regex(@"^(ERE)([\w\d]{2})(\w)(\d{2,3})$"));
            regices.Add("mac", new Regex(@"^(ERE)([\w\d]{2})(MAC)(\d{2,3})$"));
            regices.Add("device", new Regex(@"^([\d\w]{1,4})-([\w\d]{2})-([^-]*)-(.*)$"));


            if (regices["nons2008"].Match(hostname).Success)
            {
                Match nons2008Match = regices["nons2008"].Match(hostname);
                this.Business = nons2008Match.Groups[1].Value;
                this.CountryCode = nons2008Match.Groups[2].Value;
                this.LocationCode = nons2008Match.Groups[3].Value;
                this.FunctionCode = nons2008Match.Groups[4].Value;
                this.SubFunctionCode = nons2008Match.Groups[5].Value;
                this.RoleCode = nons2008Match.Groups[6].Value;
                this.ServerNumber = nons2008Match.Groups[7].Value;
                this.AdditionalText = nons2008Match.Groups[8].Value.Trim();
                nons2008Match = null;
            }
            else if (regices["workstation"].Match(hostname).Success)
            {
                Match workstationMatch = regices["workstation"].Match(hostname);
                this.Business = workstationMatch.Groups[1].Value;
                this.Platform = workstationMatch.Groups[2].Value;
                this.RoleCode = workstationMatch.Groups[3].Value;
                this.DeviceType = workstationMatch.Groups[4].Value;
                this.Instance = workstationMatch.Groups[5].Value;
                this.Owner = workstationMatch.Groups[6].Value;
                this.AssetNumber = workstationMatch.Groups[7].Value;
                this.AdditionalText = workstationMatch.Groups[8].Value.Trim();
            }
            else if (regices["workstation-xp-mac"].Match(hostname).Success)
            {
                Match workstationMatch = regices["workstation-xp-mac"].Match(hostname);
                this.Platform = workstationMatch.Groups[1].Value;
                this.Owner = workstationMatch.Groups[2].Value;
                this.AssetNumber = workstationMatch.Groups[3].Value;
                this.AdditionalText = workstationMatch.Groups[4].Value.Trim();
            }
            else if (regices["bncs"].Match(hostname).Success)
            {
                Match bncsMatch = regices["bncs"].Match(hostname);
                this.Business = bncsMatch.Groups[1].Value;
                this.LocationCode = bncsMatch.Groups[2].Value;
                this.SubFunctionCode = bncsMatch.Groups[3].Value;
                this.ServerNumber = bncsMatch.Groups[4].Value;
            }
            else if (regices["linuxnons"].Match(hostname).Success)
            {
                Match bncsMatch = regices["linuxnons"].Match(hostname);
                this.Business = bncsMatch.Groups[1].Value;
                this.LocationCode = bncsMatch.Groups[2].Value;
                this.FunctionCode = bncsMatch.Groups[3].Value;
                this.ServerNumber = bncsMatch.Groups[4].Value;
            }
            else if (regices["mac"].Match(hostname).Success)
            {
                Match bncsMatch = regices["mac"].Match(hostname);
                this.Business = bncsMatch.Groups[1].Value;
                this.LocationCode = bncsMatch.Groups[2].Value;
                this.Platform = bncsMatch.Groups[3].Value;
                this.ServerNumber = bncsMatch.Groups[4].Value;
            }
            else if (regices["device"].Match(hostname).Success)
            {
                Match bncsMatch = regices["device"].Match(hostname);
                this.Business = bncsMatch.Groups[1].Value;
                this.LocationCode = bncsMatch.Groups[2].Value;
                this.FunctionCode = bncsMatch.Groups[3].Value;
                this.ServerNumber = bncsMatch.Groups[4].Value;
            }

        }

        //server
        public string Business;
        public string CountryCode;
        public string LocationCode;
        public string FunctionCode;
        public string SubFunctionCode;
        public string RoleCode;
        public string ServerNumber;

        //workstation
        //public string Business;
        public string Platform;
        //public string RoleCode;
        public string DeviceType;
        public string Instance;
        public string Owner;
        public string AssetNumber;

        //BNCS

        public string AssetString
        {
            get
            {
                if (null != this.Owner && null != this.AssetNumber)
                {
                    return $"{this.Owner}{this.AssetNumber}";
                }
                else
                {
                    return null;
                }
            }
        }

        private string _AdditionalText;

        public string AdditionalText
        {
            get
            {
                if (null != _AdditionalText)
                    if (_AdditionalText.Length > 0)
                        return _AdditionalText;
                    else
                        return null;
                else
                    return null;
            }
            set { _AdditionalText = value; }
        }

    }

    /// Regex for NONs 2008: ^(\d)(GB|GG|JE)([A-Z0-9]{2})([\w\d]{2})(\w{2,3})?([B|\d])([A-Z0-9]{3})(.*)? 
    /// Regex for workstation: ^(\d)(\d)(\w{2})?-(D|L|V)(\d)(\w)(\d{6})(.*)
    /// Regex for BNCS: ^(ERE)([\w\d]{2,3})(BNCS)(\d{2,3})$
    /// Regex for old non MS:  ^(ERE)([\w\d]{2})(\w)(\d{2,3})$
    /// Regex for MAC: ^(ERE)([\w\d]{2})(MAC)(\d{2,3})$
    /// Regex for device: ^(ERE)-([\w\d]{2})-([^-]*)-(.*)$



}
