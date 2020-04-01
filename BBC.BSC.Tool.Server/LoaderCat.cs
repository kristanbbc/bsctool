using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BBC.BSC.Tool.Server
{
    public class LoaderCat : Loader
    {

        private readonly string catPath = @"http://cat.er.bbc.co.uk/catquery.php?json&query=";


        public LoaderCat()
        {
            //CatItem item = new CatItem
            //{
            //    Description = "test item",
            //    Hostname = "3GBV1MFDRA1001",
            //    IPAddress = "123.123.123.123",
            //    OperatingSystem = "Windows Test",
            //    catLink = new Uri(@"http://test.er.bbc.co.uk/item.123")

            //};
            //Console.WriteLine(item.nons);
            //AddToDatabase(item);
            //Console.WriteLine("ARGHHHH DID IT WORK I WONDER?");



            //List<object> items = new  List<object>();

            //items.Add(new CatItem() { Hostname = "3gbv1mfdra1a01", Description = "WG A Pan Connect" });
            //items.Add(new CatItem() { Hostname = "3gbv1mfdra1c01", Description = "WG C Pan Connect" });
            //items.Add(new CatItem() { Hostname = "3gbv1mfdra1e01", Description = "WG E Pan Connect" });

            //AddToDatabase(items);





            DumpCat();

        }
        const string cacheName = "cat";

        private List<CatResult> GetCatData()
        {
            try
            {
                if (!simpleCache.Contains(cacheName))
                {
                    Console.WriteLine("Results not in cache, retrieving new");

                    string catQuery = string.Format("{0}SELECT host_name, also_known_as, CAST(inet_ntoa(ip) as CHAR(15)) as ip, CONCAT(os,  \" \",os_version) as os FROM " +
                                       "network INNER JOIN asset ON network.asset_id = asset.asset_id " +
                                       "left join asset_os on asset.asset_id = asset_os.asset_id left join os on asset_os.os_id = os.os_id left join os_version on asset_os.os_version_id = os_version.os_version_id " +
                                       "WHERE life_cycle_status_id = 4",
                                       catPath);
                    Console.WriteLine(catQuery);
                    string json_data = string.Empty;
                    //logger.Info("Running query against CAT with\n{0}", catQuery);
                    using (WebClient w = new WebClient())
                    {
                        w.UseDefaultCredentials = true;
                        json_data = w.DownloadString(catQuery);
                    }
                    var results = !string.IsNullOrEmpty(json_data) ? JsonConvert.DeserializeObject<List<CatResult>>(json_data) : null;


                simpleCache.AddOrGetExisting("cat", results, absoluteExpiration: new DateTimeOffset(DateTime.Now.AddMinutes(30)));
                }

                Console.WriteLine("returning results");
                return (List<CatResult>)simpleCache.Get(cacheName); ;
            }
            catch (Exception ex)
            {
                Console.WriteLine("error in cat loader");
                Console.WriteLine(ex.Message);
                return null;
            }
        }


        private void DumpCat()
        {
           

            //logger.Info("Got {0} results from CAT", catRestults.Count());

            List<object> items = new List<object>();

            foreach (CatResult item in GetCatData())
            {
                items.Add(new CatItem
                {
                    Hostname = item.HostName.ToUpper(),
                    Description = item.AlsoKnownAs,
                    OperatingSystem = item.OS,
                    IPAddress = item.IP
                });
            }
            AddToDatabase(items);

        }







        private List<CatResult> catResults = new List<CatResult>();
        [Serializable]
        private class CatResult
        {
            [JsonProperty("host_name")]
            public string HostName;
            [JsonProperty("also_known_as")]
            public string AlsoKnownAs;
            [JsonProperty("ip")]
            public string IP;
            [JsonProperty("os")]
            public string OS;

        }


        class CatItem : Item
        {
            public Uri catLink;

            public CatItem()
            {
                this.Source = "CAT";
            }

        }
    }
}
