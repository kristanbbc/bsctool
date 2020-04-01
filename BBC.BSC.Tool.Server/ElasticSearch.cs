using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;

namespace BBC.BSC.Tool.Server
{
    class ElasticSearch
    {
        public ElasticClient client;

        public bool ConnectClient()
        {

            try
            {
                var node = new Uri("http://3gbv1apagw1001:9200");
                var settings = new ConnectionSettings(node);
                settings.BasicAuthentication("bsctool", "YGdq25zIgzIv19HS");
                settings.DefaultIndex("bsctool-*");
                client = new ElasticClient(settings);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
                throw;
            }

        }

    }
}
