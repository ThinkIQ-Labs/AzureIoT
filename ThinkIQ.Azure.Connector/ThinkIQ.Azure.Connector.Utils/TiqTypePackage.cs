using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqTypePackage
    {
        [JsonProperty("meta")]
        public TiqMeta Meta { get; set; } = new TiqMeta();

        [JsonProperty("enumeration_types")]
        public TiqEnumerationType[] EnumerationTypes { get; set; }

        [JsonProperty("equipment_types")]
        public TiqEquipmentType[] EquipmentTypes { get; set; }
    }
}
