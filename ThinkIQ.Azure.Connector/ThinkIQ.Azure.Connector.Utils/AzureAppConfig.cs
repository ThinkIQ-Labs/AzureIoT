namespace ThinkIQ.Azure.Connector.Utils
{
    public class AzureAppConfig
    {
        public string IoTCentralSubDomain { get; set; }
        public string IoTCentralApiToken { get; set; }
        public string IoTCentralApplicationId { get; set; }

        public string EventHubNamespaceConnectionString { get; set; }

        public string EventHubName { get; set; }
        public string BlobStorageConnectionString { get; set; }
        public string BlobContainerName { get; set; }
        public int QueryIntervalInSeconds { get; set; }
        public string ParentFqn { get; set; }
    }
}