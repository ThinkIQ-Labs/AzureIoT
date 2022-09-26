using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqAttribute
    {
        [JsonProperty("relative_name")] 
        public string Name { get; set; }

        [JsonProperty("fqn")] 
        public string[] Fqn { get; set; }

        [JsonProperty("value")] 
        public object Value { get; set; }
    }
}