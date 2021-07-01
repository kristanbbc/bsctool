using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BBC.BSC.Tool.VCenter
{
    public class VmList
    {

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
        public class Value
        {
            [JsonProperty("memory_size_MiB")]
            public int Memory_size_MiB { get; set; }
            [JsonProperty("vm")]
            public string Vm { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("power_state")]
            public string Power_state { get; set; }
            [JsonProperty("cpu_count")]
            public int Cpu_count { get; set; }
        }

        public class Root
        {
            [JsonProperty("value")]
            public List<Value> Value { get; set; }
        }
    }
}
