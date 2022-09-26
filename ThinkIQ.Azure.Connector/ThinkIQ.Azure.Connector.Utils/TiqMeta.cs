using Newtonsoft.Json;

namespace ThinkIQ.Azure.Connector.Utils
{
    public class TiqMeta
    {
        [JsonProperty("file_version")] public string FileVersion { get; set; } = "1.0.4";
    }
}
