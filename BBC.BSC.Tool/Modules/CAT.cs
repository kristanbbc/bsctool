using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBC.BSC.Tool.Modules
{  
    public class CatResult
        {
            [JsonProperty("host_name")]
            public string HostName;
            [JsonProperty("also_known_as")]
            public string AlsoKnownAs;
            [JsonProperty("ip")]
            public string Ip;
            [JsonProperty("os")]
            public string Os;

        }
    public class CAT
    {
      





    }
}
