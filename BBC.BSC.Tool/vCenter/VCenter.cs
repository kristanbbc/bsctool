using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Meziantou.Framework.Win32;
using Newtonsoft.Json;
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

        public VCenter()
        {
            //initiator

            vcenters.Add("vlrc1", new Uri("https://vcenter1.er.bbc.co.uk/rest"));
            vcenters.Add("vlrc2", new Uri("https://vcenter2.er.bbc.co.uk/rest"));


            foreach (var item in vcenters)
            {

            }
        }




        public class TokenClass
        {
            [JsonProperty("value")]
            public string Value { get; set; }
        }


        public static VmList.Root CacheResults()
        {
            RestClient restClient = new RestClient(BaseUrl);
            //TODO: test token and refresh if expired

            if (null == VCenterToken)
            {
                var existingCred = CredentialManager.ReadCredential(appName);

                if (null == existingCred)
                {
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
                    restClient.Authenticator = new HttpBasicAuthenticator(existingCred.UserName, existingCred.Password);
                }

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
            VmList.Value vm = results.Value.SingleOrDefault(v => v.Name == name);

            if (null != vm)
            {
                var startInfo = new ProcessStartInfo();


                startInfo.FileName = $"vmrc://vcent.er.bbc.co.uk/?moid={vm.Vm}";

                Process.Start(startInfo);

            }




        }
    }


}
