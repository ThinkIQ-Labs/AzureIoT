using ThinkIQ.Azure.Connector.Utils;
using ThinkIQ.DataAccess;

namespace ThinkIQ.Azure.IoT.Connector
{
    public class AppConfig
    {
        public AzureAppConfig AzureConfig { get; set; }
        public DataApi DataAccess { get; set; }
    }
}
