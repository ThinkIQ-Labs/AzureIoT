using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventHubsReceiver
{
    class Program
    {
        //<EVENT HUBS NAMESPACE - CONNECTION STRING>
        private const string ehubNamespaceConnectionString = "Endpoint=sb://tiqeventhub1ns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=***";

        // <EVENT HUB NAME>
        private const string eventHubName = "tiqeventhub1";

        // <AZURE STORAGE CONNECTION STRING>
        private const string blobStorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=tiqstorageacc;AccountKey=***;EndpointSuffix=core.windows.net";
            
        // <BLOB CONTAINER NAME>
        private const string blobContainerName = "tiqcheckpointcontainer1";


        static BlobContainerClient storageClient;

        // The Event Hubs client types are safe to cache and use as a singleton for the lifetime
        // of the application, which is best practice when events are being published or read regularly.        
        static EventProcessorClient processor;

        static async Task Main()
        {
            // Read from the default consumer group: $Default
            string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

            // Create a blob container client that the event processor will use 
            storageClient = new BlobContainerClient(blobStorageConnectionString, blobContainerName);

            // Create an event processor client to process events in the event hub
            processor = new EventProcessorClient(storageClient, consumerGroup, ehubNamespaceConnectionString, eventHubName);

            // Register handlers for processing events and handling errors
            processor.ProcessEventAsync += ProcessEventHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;

            // Start the processing
            await processor.StartProcessingAsync();

            // Wait for 30 seconds for the events to be processed
            const int delay = 3600;  // 30
            await Task.Delay(TimeSpan.FromSeconds(delay));

            // Stop the processing
            await processor.StopProcessingAsync();
        }

        static async Task ProcessEventHandler(ProcessEventArgs eventArgs)
        {
            // Write the body of the event to the console window
            var messageBody = Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());

            /*
{
  "telemetry": {
    "TruckState": "ready",
    "CoolingSystemState": "on",
    "ContentsState": "full",
    "Location": {
      "lon": -122.130137,
      "lat": 47.644702
    },
    "Event": "none",
    "ContentsTemperature": -4.87
  },
  "schema": "default@v1",
  "enrichments": {},
  "templateId": "dtmi:modelDefinition:oglodztrh:pkloiq6nto",
  "deviceId": "RefrigeratedTruck1",
  "messageSource": "telemetry",
  "enqueuedTime": "2021-11-30T23:07:51.747Z",
  "messageProperties": {
    "iothub-creation-time-utc": "2021-11-30T23:07:51.4701304Z"
  },
  "applicationId": "52876b73-1776-488d-a4fe-9e51102e9f2d"
}
             */
            

            var message = (JObject)JsonConvert.DeserializeObject(messageBody);
            if (message == null)
            {
                throw new ApplicationException("Empty message was received.");
            }

            DateTime? timeStamp = null;
            var msgProperties = message["messageProperties"];

            // Try miming iothub-creation-time-utc first
            if (msgProperties != null)
            {
                if (msgProperties["iothub-creation-time-utc"] is JValue creationTime)
                {
                    timeStamp = creationTime.Value as DateTime?;
                }
            }

            // Then try enqueuedTime
            if (timeStamp == null)
            {
                var enqueuedTime = message["enqueuedTime"] as JValue;
                if (enqueuedTime != null)
                {
                    timeStamp = enqueuedTime.Value as DateTime?;
                }
            }

            Console.WriteLine($"\tReceived event message id={eventArgs.Data.MessageId}; Timestamp={timeStamp}; data={messageBody}");
            // Update checkpoint in the blob storage so that the app receives only new events the next time it's run
            await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
        }

        static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
        {
            // Write details about the error to the console window
            Console.WriteLine($"\tPartition '{ eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
            Console.WriteLine(eventArgs.Exception.Message);
            return Task.CompletedTask;
        }
    }
}
