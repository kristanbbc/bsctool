using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Meziantou.Framework.Win32;
using Newtonsoft.Json;
using NLog;
using RestSharp;
using RestSharp.Authenticators;


namespace BBC.BSC.Tool.VCenter
{
    public class VCenter
    {
        //TODO: connect to multiple vCenters!


        private Dictionary<string, Uri> vcenters;

        private const string BaseUrl = "https://vcent.er.bbc.co.uk/rest";
        private static string VCenterToken = null;
        private const string appName = "BBC.BSC.Tool.vCenter";
        public VmList.Root cachedResults = null;
        private readonly Logger logger;

        public VCenter()
        {
            //initiator

            logger = new Logging().initLogger();

            logger.Info("Init vCenter class");
            vcenters.Add("vlrc1", new Uri("https://vcenter1.er.bbc.co.uk/rest"));
            vcenters.Add("vlrc2", new Uri("https://vcenter2.er.bbc.co.uk/rest"));


            foreach (var item in vcenters)
            {

            }
        }




        public class TokenClass
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }
        }


        public static VmList.Root CacheResults()
        {
            Logger logger = new Logging().initLogger();
            RestClient restClient = new RestClient(BaseUrl);
            //TODO: test token and refresh if expired

            if (null == VCenterToken)
            {
                logger.Info("No vCenter authentication token availble, will request new one.");
                var existingCred = CredentialManager.ReadCredential(appName);

                if (null == existingCred)
                {

                    logger.Info("No saved credentials, will request new ones.");
                    //prompt for creds if not found
                    var credsPrompt = CredentialManager.PromptForCredentials(captionText: "vCenter",
                                                                             messageText: "Authenticate to vCenter",
                                                                             saveCredential: CredentialSaveOption.Selected,
                                                                             userName: Properties.Settings.Default.ere);
                    if (credsPrompt.CredentialSaved == CredentialSaveOption.Selected)
                    {
                        CredentialManager.WriteCredential(appName, credsPrompt.UserName, credsPrompt.Password, CredentialPersistence.Session);
                    }
                    restClient.Authenticator = new HttpBasicAuthenticator(credsPrompt.UserName, credsPrompt.Password);
                }
                else
                {
                    logger.Info("Saved credentials found, will use existing ones.");
                    //TODO  - what happen if password expired - should test account is valid!
                    restClient.Authenticator = new HttpBasicAuthenticator(existingCred.UserName, existingCred.Password);
                }

                //TODO: handle error response from api (invalid login etc)
                var tokenRequest = new RestRequest("/com/vmware/cis/session", Method.POST);

                var tokenResponse = restClient.Execute(tokenRequest).Content;
                TokenClass myToken = JsonConvert.DeserializeObject<TokenClass>(tokenResponse);
                VCenterToken = myToken.Value;
            }
            var request = new RestRequest("vcenter/vm", Method.GET);
            request.AddHeader("vmware-api-session-id", VCenterToken);

            var output = restClient.Execute<VmList.Root>(request);

            return output.Data;

            //Console.WriteLine(output.Data);


            //api/vcenter/datacenter

            //// apiClient.RestClient.Execute("rest/vcenter/vm");



        }


        public static void LaunchVmrc(string name, VmList.Root results)
        {
            Logger logger = new Logging().initLogger();
            logger.Info("Searching cached vCenter results for {0} in list of {1}", name, results.Value.Count());

            VmList.Value vm = results.Value.SingleOrDefault(v => v.Name.ToLower().Trim() == name.ToLower().Trim());

            if (null != vm)
            {
                logger.Info("");
                var startInfo = new ProcessStartInfo();

                string link = $"vmrc://vcent.er.bbc.co.uk/?moid={vm.Vm}";
                logger.Trace("will launch {0}", link);
                startInfo.FileName = link;

                Process start = Process.Start(startInfo);

                if (null == start)
                {
                    logger.Warn("VMRC handler failed - probably not installed");
                    _ = MessageBox.Show("Install VMRC from the cVenter tab.", "VMRC not installed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                logger.ConditionalTrace(start);

            }



        }
    }


}
