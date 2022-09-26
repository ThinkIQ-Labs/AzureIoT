using System.Collections.Generic;
using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqEquipment
    { 
        [JsonProperty("relative_name")]
        public string Name { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("document")]
        public IDictionary<string, string> Document { get; set; }

        [JsonProperty("fqn")]
        public string[] Fqn { get; set; }

        [JsonProperty("type_fqn")]
        public string[] TypeFqn { get; set; }

        [JsonProperty("attributes")]
        public TiqAttribute[] Attributes { get; set; }
    }
}