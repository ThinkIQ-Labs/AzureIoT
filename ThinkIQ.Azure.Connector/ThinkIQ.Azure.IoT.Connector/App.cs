using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThinkIQ.Azure.Connector.Utils;
using ThinkIQ.Azure.IoT.Central.Client;
using ThinkIQ.DataAccess;

namespace ThinkIQ.Azure.IoT.Connector
{
    public class App
    {
        private const string DOMAIN = "azureiotcentral.com";
        private static readonly ILogger Logger = AppService.Instance.Provider.GetRequiredService<ILogger<App>>();
        private readonly HttpClient Client = new HttpClient();

        private System.Timers.Timer _timer;

        private AzureIoTCentral _azureIoTC;
        private readonly DataApi _dataAccess;
        private string _parentFqn;

        private readonly ConcurrentDictionary<string, string> _etagByDeviceTemplateId =
            new ConcurrentDictionary<string, string>();

        private readonly ConcurrentDictionary<string, string> _etagByDeviceId =
            new ConcurrentDictionary<string, string>();

        private EventHubsReceiver _receiver;
        private readonly AppConfig _config;
        private readonly string _libraryName;

        public App(AppConfig config)
        {
            _config = config;
            _dataAccess = _config.DataAccess;

            // Use the application id for the library name
            _libraryName = _config.AzureConfig.IoTCentralApplicationId;
        }

        public async Task StartAsync()
        {            
            Client.DefaultRequestHeaders.Add("Authorization", _config.AzureConfig.IoTCentralApiToken);

            _azureIoTC = new AzureIoTCentral(Client)
            {
                BaseUrl = $"https://{_config.AzureConfig.IoTCentralSubDomain}.{DOMAIN}"
            };

            _parentFqn = _config.AzureConfig.ParentFqn;

            // ThinkIQ
            if (string.IsNullOrWhiteSpace(_parentFqn))
            {
                var msg = $"Missing configured parent fqn {_parentFqn}.";
                Logger.LogError(msg);
                return;
            }

            // Set up for types
            var libraryTask = _dataAccess.SetupLibrary(_libraryName, _config.AzureConfig.IoTCentralSubDomain);
            if (libraryTask.IsFaulted)
            {
                Logger.LogError($"Failed to set up library {_libraryName}.");
                return;
            }

            // Set up for instances
            var parentFqnTask = _dataAccess.SetupApplicationParent(_parentFqn);
            if (parentFqnTask.IsFaulted)
            {
                Logger.LogError($"Failed to set up parent {_parentFqn} in data store.");
                return;
            }

            // Ensure type and instance system is created once before setting up telemetry acquisition
            var execTask = ExecuteAsync();
            execTask.Wait();

            // Get telemetry            
            var applicationId = _config.AzureConfig.IoTCentralApplicationId;
            _receiver = new EventHubsReceiver(
                new EventHubConfig
                {
                    NamespaceConnectionString = _config.AzureConfig.EventHubNamespaceConnectionString,
                    EventHubName = _config.AzureConfig.EventHubName,
                    BlobStorageConnectionString = _config.AzureConfig.BlobStorageConnectionString,
                    BlobContainerName = _config.AzureConfig.BlobContainerName
                },
                applicationId, 
                _dataAccess);
            await _receiver.Start();
        }

        private void Execute(object sender, EventArgs args)
        {
            try
            {
                ExecuteAsync().Wait();
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Failed to execute application.");
            }
            finally
            {
                _timer.Start();
            }
        }

        
        public void ExecuteOnTimer()
        {
            _timer = new System.Timers.Timer
            {
                Interval = _config.AzureConfig.QueryIntervalInSeconds * 1000,
                AutoReset = false
            };

            _timer.Elapsed += Execute;
            _timer.Start();
        }

        private async Task ExecuteAsync()
        {
            try
            {
                Logger.LogDebug("ThinkIQ Azure IoT application running at: {time}", DateTimeOffset.Now);

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    // For debugging purposes
                    var deviceTemplatesJson = await _azureIoTC.ListDeviceTemplates();
                    Logger.LogDebug($"Retrieved templates: {deviceTemplatesJson}.");
                }

                await GetTypes();
                await GetInstances();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to execute.");
            }
        }
        
        private async Task GetTypes()
        {
            Logger.LogDebug("Get device templates...");
            var deviceTemplates = await _azureIoTC.ListDeviceTemplatesV1Async();

            var package = new TiqTypePackage();
            var packageEnumerationTypes = new List<TiqEnumerationType>();
            var equipmentTypes = new List<TiqEquipmentType>();
            var deviceTemplateChanges = new Dictionary<string, string>();

            var deviceTemplateIdCsv = new StringBuilder();

            foreach (var deviceTemplate in deviceTemplates.Value)
            {
                if (_etagByDeviceTemplateId.TryGetValue(deviceTemplate.Id, out var etag))
                {
                    if (etag == deviceTemplate.Etag)
                    {
                        // This device template has not changed. Skip it
                        continue;
                    }
                }

                if (_etagByDeviceTemplateId.Count == 0)
                {
                    // We are just starting up. Prepare to fetch existing equipment types
                    if (deviceTemplateIdCsv.Length > 0)
                    {
                        deviceTemplateIdCsv.Append(", ");
                    }

                    deviceTemplateIdCsv.Append($"'{deviceTemplate.Id}'");
                }

                deviceTemplateChanges.Add(deviceTemplate.Id, deviceTemplate.Etag);

                var doc = new Dictionary<string, string> {{"etag", deviceTemplate.Etag}};
                var equipmentType = new TiqEquipmentType
                {
                    Document = doc,
                    Name = deviceTemplate.Id,
                    DisplayName = deviceTemplate.DisplayName,
                    Description = deviceTemplate.Description
                };

                equipmentType.Fqn = new[] { _libraryName, equipmentType.Name.ToLowerInvariant()};
                Logger.LogDebug($"Device template Id={deviceTemplate.Id}, ETag={deviceTemplate.Etag}");

                var capabilityModel = (JObject) deviceTemplate.CapabilityModel;
                var childEquipmentTypes = StructureTransform.ExtractModel(_libraryName, capabilityModel, out var enumerationTypes, out var attributeTypes);

                if (childEquipmentTypes != null && childEquipmentTypes.Count > 0)
                {
                    equipmentType.ChildEquipmentTypes = new List<TiqChildEquipmentType>();
                    foreach (var child in childEquipmentTypes)
                    { 
                        // Add child equipment types to the library
                        equipmentTypes.Add(child);

                        // Wire child equipment type reference
                        var childReference = new TiqChildEquipmentType
                        {
                            ChildTypeFqn = child.Fqn,
                            Name = child.Name,
                            DisplayName = child.DisplayName
                        };

                        equipmentType.ChildEquipmentTypes.Add(childReference);
                    }                    
                }

                // Collect enumeration types for the package
                packageEnumerationTypes.AddRange(enumerationTypes);

                // Collect attribute types for this equipment type
                equipmentType.AttributeTypes = attributeTypes;
                equipmentTypes.Add(equipmentType);
            }

            if (deviceTemplateIdCsv.Length > 0)
            {
                // Fetch existing equipment types
                var equipmentTypeTask = _dataAccess.FetchEquipmentTypes(deviceTemplateIdCsv.ToString());

                if (!equipmentTypeTask.IsFaulted)
                {
                    foreach (var kvp in equipmentTypeTask.Result)
                    {
                        // Initialize equipment types found from the store
                        _etagByDeviceTemplateId.AddOrUpdate(kvp.Key, kvp.Value, (key, oldValue) => kvp.Value);

                        // Reassess whether deviceTemplateChanges are required to be sent to the database
                        if (deviceTemplateChanges.TryGetValue(kvp.Key, out var etag))
                        {
                            if (kvp.Value == etag)
                            {
                                // This device template has not change. Remove from update list
                                deviceTemplateChanges.Remove(kvp.Key);
                            }
                        }
                    }
                }
            }

            if (deviceTemplateChanges.Count > 0)
            {
                package.EnumerationTypes = packageEnumerationTypes.ToArray();
                package.EquipmentTypes = equipmentTypes.ToArray();
                var nodeJson = JsonConvert.SerializeObject(package);
                var task = _dataAccess.SaveTypes(nodeJson);
                await task;

                if (!task.IsFaulted)
                {
                    // Update cached etag
                    foreach (var kvp in deviceTemplateChanges)
                    {
                        _etagByDeviceTemplateId.AddOrUpdate(kvp.Key, kvp.Value, (key, oldValue) => kvp.Value);
                    }
                }

                Logger.LogDebug("Created device templates");
            }
        }

        private async Task GetInstances()
        {
            var equipments = new List<TiqEquipment>();
            var devices = await _azureIoTC.ListDevicesV1Async();
            var deviceChanges = new Dictionary<string, string>();
            var deviceIdCsv = new StringBuilder();

            foreach (var device in devices.Value)
            {
                var equipmentName = device.Id.ToLowerInvariant();
                if (_etagByDeviceId.TryGetValue(equipmentName, out var etag))
                {
                    if (etag == device.Etag)
                    {
                        // This device has not changed. Skip it
                        continue;
                    }
                }

                deviceChanges.Add(equipmentName, device.Etag);

                var equipment = new TiqEquipment
                {
                    Name = equipmentName,
                    DisplayName = device.DisplayName,
                    TypeFqn = new[] { _libraryName, device.Template.ToLowerInvariant()},
                    Document = new Dictionary<string, string> {{"etag", device.Etag}}
                };
                
                var parentNameParts = _parentFqn.Split('.');
                var nameParts = new List<string>(parentNameParts) { equipment.Name };
                equipment.Fqn = nameParts.ToArray();

                if (_etagByDeviceId.Count == 0)
                {
                    // We are just starting up. Prepare to fetch existing equipment instances
                    if (deviceIdCsv.Length > 0)
                    {
                        deviceIdCsv.Append(", ");
                    }

                    deviceIdCsv.Append("array[");
                    foreach (var parentNamePart in parentNameParts)
                    {
                        deviceIdCsv.Append($"'{parentNamePart}',");
                    }

                    deviceIdCsv.Append($"'{equipment.Name}']");
                }


                // Get configuration attributes
                // Properties (read/write). Example: TruckId is readonly. Optimal temperature is writable
                var deviceProperties = await _azureIoTC.GetDevicePropertiesV1Async(device.Id, device.Template);

                // Skip meta data
                var attributes = new List<TiqAttribute>();
                deviceProperties.AdditionalProperties.Remove("$metadata");
                foreach (var propPair in deviceProperties.AdditionalProperties)
                {
                    var name = propPair.Key.ToLowerInvariant();
                    var fqn = new List<string>(equipment.Fqn) {name};
                    var attrib = new TiqAttribute
                    {
                        Name = name,
                        Fqn = fqn.ToArray(),
                        Value = propPair.Value
                    };

                    attributes.Add(attrib);
                }

                equipment.Attributes = attributes.ToArray();
                equipments.Add(equipment);
            }

            if (deviceIdCsv.Length > 0)
            {
                // Fetch existing equipment instances
                var equipmentTask = _dataAccess.FetchEquipmentInstances(deviceIdCsv.ToString());
                if (!equipmentTask.IsFaulted)
                {
                    foreach (var kvp in equipmentTask.Result)
                    {
                        // Initialize equipment instances found from the store
                        _etagByDeviceId.AddOrUpdate(kvp.Key, kvp.Value, (key, oldValue) => kvp.Value);

                        // Reassess whether deviceChanges are required to be sent to the database
                        if (deviceChanges.TryGetValue(kvp.Key, out var etag))
                        {
                            if (kvp.Value == etag)
                            {
                                // This device has not change. Remove from update list
                                deviceChanges.Remove(kvp.Key);
                            }
                        }
                    }
                }
            }

            if (deviceChanges.Count > 0)
            {
                var instanceJson = JsonConvert.SerializeObject(equipments);
                var instanceTask = _dataAccess.SaveInstances(instanceJson);
                await instanceTask;

                if (!instanceTask.IsFaulted)
                {
                    foreach (var kvp in deviceChanges)
                    {
                        _etagByDeviceId.AddOrUpdate(kvp.Key, kvp.Value, (key, oldValue) => kvp.Value);
                    }
                }
            }
        }

        public async Task StopAsync()
        {
            _timer.Elapsed -= Execute;
            _timer = null;
            await _receiver.Stop();
        }
    }
}