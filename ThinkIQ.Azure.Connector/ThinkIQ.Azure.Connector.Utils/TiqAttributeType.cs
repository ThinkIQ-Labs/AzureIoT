using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqAttributeType
    {
        [JsonProperty("relative_name")] 
        public string Name { get; set; }

        [JsonProperty("display_name")] 
        public string DisplayName { get; set; }

        [JsonProperty("description")] 
        public string Description { get; set; }

        [JsonProperty("data_type")]
        public string DataType { get; set; }

        [JsonProperty("source_category")] 
        public string SourceCategory { get; set; }

        [JsonProperty("default_measurement_unit_fqn")]
        public string[] MeasurementUnitFqn { get; set; }

        [JsonProperty("enumeration_type_fqn")] 
        public string[] EnumerationTypeFqn { get; set; }

        [JsonProperty("default_enumeration_values")]
        public string[] DefaultEnumerationValues { get; set; }
    }
}