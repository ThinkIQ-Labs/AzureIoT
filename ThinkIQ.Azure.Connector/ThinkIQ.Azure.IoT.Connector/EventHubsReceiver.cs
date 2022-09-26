using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NpgsqlTypes;
using ThinkIQ.Azure.Connector.Utils;
using ThinkIQ.DataAccess;

namespace ThinkIQ.Azure.IoT.Connector
{
    public class EventHubsReceiver
    {
        private static readonly ILogger Logger = AppService.Instance.Provider.GetRequiredService<ILogger<EventHubsReceiver>>();
        private BlobContainerClient storageClient;
        private EventProcessorClient processor;

        private readonly EventHubConfig _config;
        private readonly string _applicationId;
        private readonly DataApi _dataApi;

        // Dictionary of attribute device_template|device|property
        private readonly ConcurrentDictionary<string, AttribInfo> _attribCache = new ConcurrentDictionary<string, AttribInfo>();

        public EventHubsReceiver(EventHubConfig config, string applicationId, DataApi dataApi)
        {
            _config = config;
            _applicationId = applicationId;
            _dataApi = dataApi;
        }

        public async Task Start()
        {
            // Read from the default consumer group: $Default
            var consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

            // Create a blob container client that the event processor will use 
            storageClient = new BlobContainerClient(
                _config.BlobStorageConnectionString, // <AZURE STORAGE CONNECTION STRING>
                _config.BlobContainerName); // <BLOB CONTAINER NAME>

            // Create an event processor client to process events in the event hub

            processor = new EventProcessorClient(storageClient, consumerGroup,
                _config.NamespaceConnectionString, // <EVENT HUBS NAMESPACE - CONNECTION STRING>
                _config.EventHubName // <EVENT HUB NAME>
            );

            // Register handlers for processing events and handling errors
            processor.ProcessEventAsync += ProcessEventHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;

            // Start the processing
            await processor.StartProcessingAsync();
        }

        public async Task Stop()
        {
            // Stop the processing
            await processor.StopProcessingAsync();
        }

        private async Task ProcessEventHandler(ProcessEventArgs eventArgs)
        {
            // Write the body of the event to the console window
            var messageBody = Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());

            /* Top-level device telemetry
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

            /* Component telemetry
            {
              "applicationId": "52876b73-1776-488d-a4fe-9e51102e9f2d",
              "component": "RefrigeratedTruck_3s9",
              "deviceId": "Refrigerated_Truck_2",
              "enqueuedTime": "2022-03-11T20:15:41.967Z",
              "enrichments": {},
              "messageProperties": {
                "iothub-creation-time-utc": "2022-03-11T20:15:41.927Z"
              },
              "messageSource": "telemetry",
              "schema": "default@v1",
              "telemetry": {
                "ContentsTemperature": 14.225031634300917
              },
              "templateId": "dtmi:modelDefinition:oglodztrh:pkloiq6nto"
            }
            */

            var message = (JObject)JsonConvert.DeserializeObject(messageBody);
            if (message == null)
            {
                throw new ApplicationException("Empty message was received.");
            }

            if (!message.ContainsKey("applicationId"))
            {
                Logger.LogDebug($"Receive telemetry without application ID");
                return;
            }

            var receivedApplicationId = message["applicationId"].ToString();
            if (receivedApplicationId != _applicationId)
            {
                Logger.LogDebug($"Receive telemetry from application ID {receivedApplicationId} on application ID {_applicationId}.");
                return;
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

            var equipmentTypeName = (string)message["templateId"];

            var deviceId = (string)message["deviceId"];
            var equipmentName = deviceId.ToLowerInvariant();

            var childEquipmentName = (string)message["component"];
            if (childEquipmentName == null)
            {
                // Still to be used in attribString as key
                childEquipmentName = string.Empty;
            }
            else
            {
                childEquipmentName = childEquipmentName.ToLowerInvariant();
            }

            var telemetry = message["telemetry"];

            // Define variables for each data type
            var boolAttribIds = new List<long>();
            var boolValues = new List<object>();
            var boolTimeStamps = new List<DateTime>();

            var intAttribIds = new List<long>();
            var intValues = new List<object>();
            var intTimeStamps = new List<DateTime>();

            var floatAttribIds = new List<long>();
            var floatValues = new List<object>();
            var floatTimeStamps = new List<DateTime>();

            var stringAttribIds = new List<long>();
            var stringValues = new List<object>();
            var stringTimeStamps = new List<DateTime>();

            var dateTimeAttribIds = new List<long>();
            var dateTimeValues = new List<object>();
            var dateTimeTimeStamps = new List<DateTime>();

            var objectAttribIds = new List<long>();
            var objectValues = new List<object>();
            var objectTimeStamps = new List<DateTime>();

            var geopointAttribIds = new List<long>();
            var geopointValues = new List<object>();
            var geopointTimeStamps = new List<DateTime>();

            // Exact attributes into separate type groups
            foreach (var attrib in telemetry)
            {
                if (!(attrib is JProperty prop))
                {
                    throw new InvalidDataException($"{attrib} is not a Json property.");
                }

                
                var attributeName = prop.Name.ToLowerInvariant();

                var attribString = $"{equipmentTypeName}|{equipmentName}|{childEquipmentName}|{attributeName}";

                if (!_attribCache.TryGetValue(attribString, out var attribInfo))
                {
                    var attribTask = _dataApi.GetAttributeInfo(equipmentTypeName, equipmentName, childEquipmentName, attributeName);
                    await attribTask;
                    if (!attribTask.IsFaulted && attribTask.Result != null)
                    {
                        attribInfo = attribTask.Result;
                        _attribCache.AddOrUpdate(attribString, attribInfo, (key, oldValue) => attribInfo);
                    }
                    else
                    {
                        Logger.LogWarning($"Receive telemetry for unknown attribute {attributeName} for child equipment {childEquipmentName} under {equipmentName} of type {equipmentTypeName}");
                        continue;
                    }
                }

                object objValue = GetAttributeValue(prop, attribInfo.DataType);

                switch (attribInfo.DataType)
                {
                    case DataType.Bool:
                        boolAttribIds.Add(attribInfo.Id);
                        boolValues.Add(objValue);
                        boolTimeStamps.Add(timeStamp.Value);
                        break;

                    case DataType.Int:
                        intAttribIds.Add(attribInfo.Id);
                        intValues.Add(objValue);
                        intTimeStamps.Add(timeStamp.Value);
                        break;

                    case DataType.Float:
                        floatAttribIds.Add(attribInfo.Id);
                        floatValues.Add(objValue);
                        floatTimeStamps.Add(timeStamp.Value);
                        break;

                    case DataType.String:
                    case DataType.Enumeration:
                        stringAttribIds.Add(attribInfo.Id);
                        stringValues.Add(objValue);
                        stringTimeStamps.Add(timeStamp.Value);
                        break;

                    case DataType.DateTime:
                        dateTimeAttribIds.Add(attribInfo.Id);
                        dateTimeValues.Add(objValue);
                        dateTimeTimeStamps.Add(timeStamp.Value);
                        break;

                    case DataType.Object:
                        objectAttribIds.Add(attribInfo.Id);
                        objectValues.Add(objValue);
                        objectTimeStamps.Add(timeStamp.Value);
                        break;

                    case DataType.Geopoint:
                        geopointAttribIds.Add(attribInfo.Id);
                        geopointValues.Add(objValue);
                        geopointTimeStamps.Add(timeStamp.Value);
                        break;

                    default:
                        throw new InvalidDataException($"Invalid data type {attribInfo.DataType}");
                }

                Logger.LogDebug($"{prop.Name} = {objValue}");
            }

            var boolTask = _dataApi.UpsertTimeSeriesArray(DataType.Bool, boolAttribIds, boolValues, boolTimeStamps);
            var intTask = _dataApi.UpsertTimeSeriesArray(DataType.Int, intAttribIds, intValues, intTimeStamps);
            var floatTask =
                _dataApi.UpsertTimeSeriesArray(DataType.Float, floatAttribIds, floatValues, floatTimeStamps);
            var stringTask =
                _dataApi.UpsertTimeSeriesArray(DataType.String, stringAttribIds, stringValues, stringTimeStamps);
            var dateTimeTask = _dataApi.UpsertTimeSeriesArray(DataType.DateTime, dateTimeAttribIds, dateTimeValues,
                dateTimeTimeStamps);
            var objectTask =
                _dataApi.UpsertTimeSeriesArray(DataType.Object, objectAttribIds, objectValues, objectTimeStamps);
            var geopointTask = _dataApi.UpsertTimeSeriesArray(DataType.Geopoint, geopointAttribIds, geopointValues, geopointTimeStamps);

            await boolTask;
            await intTask;
            await floatTask;
            await stringTask;
            await dateTimeTask;
            await objectTask;
            await geopointTask;

            Logger.LogDebug($"\tReceived event message id={eventArgs.Data.MessageId}; Timestamp={timeStamp}; data={messageBody}");

            // Update checkpoint in the blob storage so that the app receives only new events the next time it's run
            await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
        }

        private object GetAttributeValue(JProperty prop, DataType dataType)
        {
            object objValue;
           
            if (prop.Value is JValue jValue)
            {
                // Primitive property
                objValue = jValue.Value;
            }
            else if (prop.Value is JObject jObj)
            {
                if (dataType == DataType.Geopoint)
                {
                    // Geolocation property
                    var lon = Convert.ToDouble(jObj["lon"]);
                    var lat = Convert.ToDouble(jObj["lat"]);
                    objValue = new NpgsqlPoint(lon, lat);
                }
                else
                {
                    // We need to call ToString() to form json
                    objValue = prop.Value.ToString();
                }
            }
            else
            {
                throw new InvalidDataException($"Invalid property data type {prop.Value.Type}");
            }

            return objValue;
        }

        static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
        {
            Logger.LogError($"Partition '{eventArgs.PartitionId}': an unhandled exception was encountered. {eventArgs.Exception.Message}");
            return Task.CompletedTask;
        }
    }
}