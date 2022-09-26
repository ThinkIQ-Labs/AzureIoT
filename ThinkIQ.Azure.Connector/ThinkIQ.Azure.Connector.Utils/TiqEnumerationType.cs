using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqEnumerationType
    {
        [JsonProperty("fqn")]
        public string[] Fqn { get; set; }

        [JsonProperty("relative_name")]
        public string Name { get; set; }

        [JsonProperty("enumeration_names")]
        public string[] EnumerationNames { get; set; }

        [JsonProperty("default_enumeration_values")]
        public string[] DefaultEnumerationValues { get; set; }
    }
}
