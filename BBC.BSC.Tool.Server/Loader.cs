using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using Nest;


namespace BBC.BSC.Tool.Server
{

    interface ILoader
    {
        void AddToDatabase(object temp);

    }
    public class Loader : ILoader, IDisposable
    {


        private ElasticSearch elasticSearch;
        public FileCache simpleCache = new FileCache(FileCacheManagers.Hashed);

        public Loader()
        {
            elasticSearch = new ElasticSearch();
            elasticSearch.ConnectClient();
        }
        public void AddToDatabase(List<object> temps)
        {

            var response = elasticSearch.client.IndexManyAsync(temps, $"bsctool-{((Item)temps[0]).Source.ToLower()}");
            Console.WriteLine(response.Result);
        }

        public void AddToDatabase(object temp)
        {
            //elasticSearch.ConnectClient();

            Console.WriteLine(((Item)temp).Hostname);
            Console.WriteLine(((Item)temp).Source);
            Console.WriteLine(((Item)temp).Description);

            var response = elasticSearch.client.Index(temp, idx => idx.Index($"bsctool-{((Item)temp).Source.ToLower()}"));
            Console.WriteLine(response.Result);



            //does something
        }

        public List<Item> Search(string input)
        {
            var searchResponse = elasticSearch.client.Search<Item>(s => s
            .Query(q => q
            .MultiMatch(c => c
            .Fields(f => f
            .Field("hostname", 50)
            .Field("hostname.ngrams")
            .Field("nons.assetString", 40)
            .Field("nons.assetString.ngrams", 4)
            .Field("nons.assetNumber")
            .Field("description")
            )
            .Query(input)
            .Type(TextQueryType.BestFields)
            )
            ))
                ;


            /*
             * {
    
    "query": {
    "multi_match" : {
      "query":    "33481", 
      "fields": [ 
          "hostname^50", 
      "hostname.ngrams" ,
      "nons.assetString^40" ,
      "nons.assetString.ngrams^4" ,
      "nons.assetNumber",
      "description"],
      "type": "best_fields"
    }
    },
        "highlight": {
            "fields" : {
                "hostname" : {},
                "hostname.ngrams" : {},
                "description" : {},
                 "nons.assetString": {},
                 "nons.assetString.ngrams": {},
                 "nons.assetNumber": {}
            }
        }
  
    
}

    */


            return (List<Item>)searchResponse.Documents;
        }


        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
