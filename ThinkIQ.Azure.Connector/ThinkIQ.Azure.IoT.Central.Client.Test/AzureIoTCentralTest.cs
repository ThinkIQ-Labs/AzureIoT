using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using ThinkIQ.Azure.Connector.Utils;
using ThinkIQ.Azure.IoT.Connector;
using Xunit;
using Xunit.Sdk;

namespace ThinkIQ.Azure.IoT.Central.Client.Test
{
    public class AzureIoTCentralTest : IClassFixture<BaseTest>
    {
        private static readonly string s_dtdlPath = Path.Combine(GetWorkingDirectory(), "DTDL");
        private readonly DataAccess.DataApi _dataAccess;

        public AzureIoTCentralTest(BaseTest baseTest)
        {
            AppService.Instance.Initialize(baseTest.Host.Services);
            var configuration = AppService.Instance.Provider.GetRequiredService<IConfiguration>();
            var dataAccessSection = configuration.GetSection("DataAccess");
            var dataAccessConfig = dataAccessSection.Get<DataAccessConfig>();
            _dataAccess = new DataAccess.DataApi(dataAccessConfig.ConnectionString);
        }

        [Fact]
        public void TestDeserializeDeviceTemplate()
        {
            var templateFile = Path.Combine(s_dtdlPath, "refrigeratedtruck_template.json");
            var modelString = File.ReadAllText(templateFile);
            var deviceTemplates = JsonConvert.DeserializeObject<DeviceTemplateCollectionV1>(modelString);
            Assert.NotNull(deviceTemplates);
            Assert.Equal(1, deviceTemplates.Value.Count);
            var enumerator = deviceTemplates.Value.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                throw new XunitException("Failed to get device template");
            }

            var deviceTemplate = enumerator.Current;
            Assert.NotNull(deviceTemplate);
            var capabilityModel = (JObject)deviceTemplate.CapabilityModel;
            var libraryName = Guid.NewGuid().ToString();
            StructureTransform.ExtractModel(libraryName, capabilityModel, out var enumerationTypes, out var attributeTypes);

            Assert.Equal(3, enumerationTypes.Count);
            Assert.Equal(8, attributeTypes.Count);
        }

        [Fact]
        public void TestDeserializeDeviceTemplateWithoutExtends()
        {
            var templateFile = Path.Combine(s_dtdlPath, "refrigeratedtruck_no_extends_template.json");
            var modelString = File.ReadAllText(templateFile);
            var deviceTemplates = JsonConvert.DeserializeObject<DeviceTemplateCollectionV1>(modelString);
            Assert.NotNull(deviceTemplates);
            Assert.Equal(1, deviceTemplates.Value.Count);
            var enumerator = deviceTemplates.Value.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                throw new XunitException("Failed to get device template");
            }

            var deviceTemplate = enumerator.Current;
            Assert.NotNull(deviceTemplate);
            var capabilityModel = (JObject)deviceTemplate.CapabilityModel;
            var libraryName = Guid.NewGuid().ToString();
            StructureTransform.ExtractModel(libraryName, capabilityModel, out var enumerationTypes, out var attributeTypes);

            Assert.Equal(3, enumerationTypes.Count);
            Assert.Equal(8, attributeTypes.Count);
        }

        [Fact]
        public void TestDeserializeDeviceTemplateWithComponents()
        {
            var templateFile = Path.Combine(s_dtdlPath, "IoT_Plug_and_Play_mobile_template.json");
            var modelString = File.ReadAllText(templateFile);
            var deviceTemplates = JsonConvert.DeserializeObject<DeviceTemplateCollectionV1>(modelString);
            Assert.NotNull(deviceTemplates);
            Assert.Equal(1, deviceTemplates.Value.Count);
            var enumerator = deviceTemplates.Value.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                throw new XunitException("Failed to get device template");
            }

            var deviceTemplate = enumerator.Current;
            Assert.NotNull(deviceTemplate);
            var capabilityModel = (JObject)deviceTemplate.CapabilityModel;
            var libraryName = Guid.NewGuid().ToString();
            var childEquipmentTypes = StructureTransform.ExtractModel(libraryName, capabilityModel, out var enumerationTypes, out var attributeTypes);
            Assert.Equal("\"~JMiCah2FR9OId/DLHDECwSrEreKvQ2fe89JwAke5+7o=\"", deviceTemplate.Etag);
            Assert.Equal(0, enumerationTypes.Count);            
            Assert.Equal(2, attributeTypes.Count);
            Assert.Equal(2, childEquipmentTypes.Count);
            Assert.Equal(6, childEquipmentTypes[0].AttributeTypes.Count);
            Assert.Equal(8, childEquipmentTypes[1].AttributeTypes.Count);
        }

        [Fact]
        public void TestDeserializeDeviceInstance()
        {
            var instanceFile = Path.Combine(s_dtdlPath, "refrigeratedtruck1_instance.json");
            var modelString = File.ReadAllText(instanceFile);
            var instances = JsonConvert.DeserializeObject<DeviceCollectionV1>(modelString);
            Assert.NotNull(instances);
        }

        [Fact]
        public void TestDeserializeDevice()
        {
            var propertiesFile = Path.Combine(s_dtdlPath, "refrigeratedtruck1_properties.json");
            var modelString = File.ReadAllText(propertiesFile);
            var properties = JsonConvert.DeserializeObject<DevicePropertiesV1>(modelString);
            Assert.Equal(3, properties.AdditionalProperties.Count);
        }

        [Fact]
        public void TestDeserializeCapabilityModel()
        {
            var capabilityFolder = Path.Combine(s_dtdlPath, "Capability");

            foreach (var filePath in Directory.GetFiles(capabilityFolder, "*.json"))
            {
                var modelString = File.ReadAllText(filePath);
                try
                {
                    var typedObject = JsonConvert.DeserializeObject<Capability>(modelString);
                    Assert.True(typedObject != null);

                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    var message = "Could not deserialize the response body string as " +
                                  typeof(DeviceTemplate).FullName +
                                  ".";
                    throw new Exception(message, ex);
                }
            }
        }

        private static string GetWorkingDirectory()
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }
    }
}