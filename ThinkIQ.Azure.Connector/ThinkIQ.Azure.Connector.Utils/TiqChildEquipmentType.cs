using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqChildEquipmentType
    {   
        [JsonProperty("relative_name")]
        public string Name { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("child_type_fqn")]
        public string[] ChildTypeFqn { get; set; }
    }
}
