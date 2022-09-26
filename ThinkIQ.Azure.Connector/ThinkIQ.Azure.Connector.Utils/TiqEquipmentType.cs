using System.Collections.Generic;
using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqEquipmentType
    {
        [JsonProperty("fqn")]
        public string[] Fqn { get; set; }

        [JsonProperty("relative_name")]
        public string Name { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("description")]

        public string Description { get; set; }
        [JsonProperty("document")]
        public IDictionary<string, string> Document { get; set; }

        [JsonProperty("attributes")]
        public IList<TiqAttributeType> AttributeTypes { get; set; }

        [JsonProperty("child_equipment")]
        public IList<TiqChildEquipmentType> ChildEquipmentTypes { get; set; }

    }
}
