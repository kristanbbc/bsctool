using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nest;

namespace BBC.BSC.Tool.Server
{
    public partial class svcServer : ServiceBase
    {

        public ElasticClient client;

        public svcServer()
        {
            InitializeComponent();



        }

        protected override void OnStart(string[] args)
        {
            Console.WriteLine("Start of OnStart");
            try
            {
                var node = new Uri("http://3gbv1apagw1001:9200");
                var settings = new ConnectionSettings(node);
                settings.BasicAuthentication("bsctool", "YGdq25zIgzIv19HS");
                client = new ElasticClient(settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }


            var tweet = new Tweet
            {
                Id = DateTime.Now.Second,
                User = "kimchy",
                PostDate = new DateTime(),
                Message = "Trying out NEST, so far so good?"
            };

            var response = client.Index(tweet, idx => idx.Index("bsctool-testdata")); //or specify index via settings.DefaultIndex("mytweetindex");
            Console.WriteLine(response.Result);


            Thread.Sleep(5000);

            var searchresponse = client.Search<Tweet>(s => s
    .Index("bsctool-testdata") //or specify index via settings.DefaultIndex("mytweetindex");
    .From(0)
    .Size(10)
    .Query(q => q
        .Term(t => t.User, "kimchy") || q
        .Match(mq => mq.Field(f => f.User).Query("nest"))
    )
);
            Console.WriteLine(searchresponse.Hits.Count);

            foreach (var item in searchresponse.Hits)
            {
                Console.WriteLine($"{item.Id} : {item.Source.User} : {item.Source.Message}");
            }

            Console.WriteLine("End of OnStart");
        }

        protected override void OnStop()
        {
            Console.WriteLine("Start of OnStop");
            

            Console.WriteLine("End of OnStop");
        }
    }

    internal class Tweet
    {
        public int Id { get; set; }
        public string User { get; set; }
        public DateTime PostDate { get; set; }
        public string Message { get; set; }
    }
}
