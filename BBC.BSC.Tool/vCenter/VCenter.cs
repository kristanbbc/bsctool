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


        private const string BaseUrl = "https://vcent.er.bbc.co.uk/rest";
        static string VCenterToken = null;
        private const string appName = "BBC.BSC.Tool.vCenter";
        public vCenter.VmList.Root cachedResults = null;

        public VCenter()
        {
            //initiator

        }

        public class TokenClass
        {
            [JsonProperty("value")]
            public string Value { get; set; }
        }


        public static vCenter.VmList.Root CacheResults()
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

            var output = restClient.Execute<vCenter.VmList.Root>(request);

            return output.Data;

            //Console.WriteLine(output.Data);


            //api/vcenter/datacenter

            //// apiClient.RestClient.Execute("rest/vcenter/vm");



        }


        public static void LaunchVmrc(string name, vCenter.VmList.Root results)
        {
            vCenter.VmList.Value vm = results.Value.SingleOrDefault(v => v.Name == name);

            if (null != vm)
            {
                var startInfo = new ProcessStartInfo();


                startInfo.FileName = $"vmrc://vcent.er.bbc.co.uk/?moid={vm.Vm}";

                Process.Start(startInfo);

            }




        }
    }


}
