namespace ThinkIQ.Azure.Connector.Utils
{
    public class EventHubConfig
    {
        public string NamespaceConnectionString { get; set; }
        public string EventHubName { get; set; }
        public string BlobStorageConnectionString { get; set; }
        public string BlobContainerName { get; set; }
    }
}