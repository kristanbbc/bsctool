using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.DirectoryServices;

namespace BBC.BSC.Tool.Server
{
    class LoaderAD : Loader
    {
        //ADItem myItem = new ADItem() { Source = "AD" };
        private readonly string ldapPath = @"LDAP://ldap.national.core.bbc.co.uk";

        public LoaderAD()
        {
            //myItem.Hostname = "31-D0N334810";
            //myItem.Description = "Kristan's old laptop";
            //myItem.OperatingSystem = "Windows 7";

            //AddToDatabase(myItem);
            //Console.WriteLine(myItem.nons);



            DumpAd();






        }
        static readonly string cacheName = "ad";

        private void DumpAd()
        {
            List<ADItem> AdItems = GetAdData();
            if (null != AdItems)
            {
                int elasticPageSize = 5000;
                List<object> items = new List<object>();
                //logger.Info("Found {0} results in Active Directory", sResults.Count);
                Console.WriteLine("Found {0} results in Active Directory", AdItems.Count);
                int i = 0;
                int x = 0;

                foreach (var item in AdItems)
                {
                    i++;
                    //logger.ConditionalTrace("AD: found: {0}", item.Properties["name"][0].ToString().ToUpper());
                    items.Add(item);
                    if (i > elasticPageSize)
                    {
                        x++;
                        Console.WriteLine(x * elasticPageSize);
                        AddToDatabase(items);
                        i = 0;
                        items.Clear();
                    }
                }
                //add remaining items from last page - must be a better way of doing this!
                AddToDatabase(items);
            }
            else
            {
                Console.WriteLine("ERROR getting AD results - result null");
            }

        }




        private List<ADItem> GetAdData()
        {
            if (!simpleCache.Contains(cacheName))
            {
                Console.WriteLine("Cache doesn't contain AD results, getting new");
                try
                {

                    using (DirectoryEntry dEntry = new DirectoryEntry(ldapPath))
                    using (DirectorySearcher dSearcher = new DirectorySearcher(dEntry)
                    {
                        // (|(cn=*334810*)(displayname=*334810*)(cn=PC-*334810*)(cn=B1-D0*334810*)(cn=B1-L0*334810*)(cn=61-D0*334810*)(cn=61-L0*334810*)(cn=71-D0*334810*)(cn=71-L0*334810*)(cn=91-D0*334810*)(cn=91-L0*334810*)(cn=F1-D0*334810*)(cn=F1-L0*334810*)(cn=MC-*334810*)(sn=*334810*)(samAccountName=*334810*)(mail=*334810*)(proxyaddresses=smtp:*334810*)(ou=*334810*)(&(objectcategory=printqueue)(printername=*334810*)))
                        //Filter = string.Format("(&(objectClass=computer)(cn={0}*))", e.Argument.ToString()),

                        // removed wildcard so only collect known patterns (cn={0}*)(displayname={0}*)
                        Filter = string.Format("(&(!userAccountControl:1.2.840.113556.1.4.803:=2)(objectClass=computer)(|(cn=PC-{0}*)(cn=B1-D0{0}*)(cn=B1-L0{0}*)(cn=31-D0{0}*)(cn=31*-D0{0}*)(cn=61-D0{0}*)(cn=61-L0{0}*)(cn=71-D0{0}*)(cn=71-L0{0}*)(cn=91-D0{0}*)(cn=91-L0{0}*)(cn=F1-D0{0}*)(cn=F1-L0{0}*)(cn=MC-{0}*)(sn={0}*)(samAccountName={0}*)))", ""),
                        PageSize = 500,
                        //ServerTimeLimit = TimeSpan.FromSeconds(15),
                        //ServerPageTimeLimit = TimeSpan.FromSeconds(15),
                        //SizeLimit = 20,
                        ClientTimeout = TimeSpan.FromSeconds(300)
                    })
                    {
                        dSearcher.PropertiesToLoad.Clear();
                        dSearcher.PropertiesToLoad.Add("name");
                        dSearcher.PropertiesToLoad.Add("description");
                        dSearcher.PropertiesToLoad.Add("operatingsystem");
                        using (SearchResultCollection sResults = dSearcher.FindAll())
                        {
                            items.Clear();
                            foreach (SearchResult item in sResults)
                            {
                                items.Add(new ADItem()
                                {
                                    Hostname = item.Properties["name"][0].ToString().ToUpper(),
                                    Description = CleanResultProperty(item, "description"),
                                    OperatingSystem = CleanResultProperty(item, "operatingSystem"),

                                });
                            }
                            simpleCache.AddOrGetExisting(cacheName, items, absoluteExpiration: new DateTimeOffset(DateTime.Now.AddHours(8)));
                            items.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Trace.TraceError(ex.Message);
                    //logger.Warn("LDAP query error: {0}", ex.Message);
                    Console.Write("LDAP ERROR: {0}", ex.Message);
                    return null;
                }
            }
            return (List<ADItem>)simpleCache.Get(cacheName);
        }

        List<ADItem> items = new List<ADItem>();

        [Serializable]
        class ADItem : Item
        {
            public ADItem()
            {
                Source = "AD";
            }
            //public new readonly string Source = "AD";
        }

        private static string CleanResultProperty(SearchResult item, string property)
        {
            return (item.Properties.Contains(property) ? item.Properties[property][0].ToString() : "");
        }

    }
}

