using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThinkIQ.Azure.Connector.Utils;

namespace ThinkIQ.DataAccess
{
    public class DataApi
    {
        private static readonly ILogger Logger = AppService.Instance.Provider.GetRequiredService<ILogger<DataApi>>();
        private readonly string _connectionString;

        public DataApi(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task SetupLibrary(string libraryName, string displayName)
        {
            try
            {
                var description = $"Library to host type system from Azure IoT Application {libraryName}";
                var cmdText =
$@"do
$$
begin
    if not exists (select null from model.libraries where relative_name = '{libraryName}') then
	    insert into model.libraries (relative_name, display_name, description)
	    values ('{libraryName}', '{displayName}', '{description}');
    end if;
end;
$$";

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.Text };
                
                await cmd.PrepareAsync();
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to set up library {libraryName}");
                throw;
            }
        }

        public async Task<long> SetupApplicationParent(string parentFqn)
        {
            try
            {
                const string cmdText = "model.get_or_create_equipment_parent";

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_parent_fqn", NpgsqlDbType.Text, parentFqn);

                await cmd.PrepareAsync();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    return (long)result;
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to set up parent for equipment from fqn {parentFqn}");
                throw;
            }

            throw new ApplicationException($"Failed to get parent id for equipment from fqn {parentFqn}");
        }

        public async Task SaveTypes(string nodes)
        {
            try
            {
                const string cmdText = "model.import_type_system";

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_nodes", NpgsqlDbType.Jsonb, nodes);
                cmd.Parameters.AddWithValue("@p_mode", NpgsqlDbType.Text, "UPDATE");

                await cmd.PrepareAsync();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    Logger.LogDebug(result.ToString());
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to import type system");
                throw;
            }
        }

        public async Task SaveInstances(string instancePackageJson)
        {
            try
            {
                // templateName = dtmi:modelDefinition:oglodztrh:pkloiq6nto
                const string cmdText = "model.import_equipment";

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@p_root_node_id", NpgsqlDbType.Bigint, DBNull.Value);
                cmd.Parameters.AddWithValue("@p_nodes", NpgsqlDbType.Jsonb, instancePackageJson);

                // The attributes should be created via propagation from type_to_attribute_type.
                // We just need to update configuration attributes.
                cmd.Parameters.AddWithValue("@p_mode", NpgsqlDbType.Text, "UPDATE");

                await cmd.PrepareAsync();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    Logger.LogDebug($"Instance result: {result}.");
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to import equipment");
                throw;
            }
        }

        internal static NpgsqlConnection OpenConnection(string _connectionString)
        {
            try
            {
                var conn = new NpgsqlConnection(_connectionString);
                var isPooling = !_connectionString.ToUpper().Contains("POOLING=FALSE");
                {
                    Logger.LogDebug("Pooling: {0}", isPooling);
                }

                Logger.LogDebug(
                    $"Host: {conn.Host}; Database: {conn.Database}; UserName: {conn.UserName}; Port: {conn.Port}; CommandTimeout: {conn.CommandTimeout}; ConnectionTimeout: {conn.ConnectionTimeout}; Pooling: {isPooling}");

                var task = conn.OpenAsync();
                task.Wait();

                Logger.LogDebug($"PostgresSqlVersion: {conn.PostgreSqlVersion}; ServerVersion: {conn.ServerVersion}");

                foreach (var parameter in conn.PostgresParameters)
                {
                    Logger.LogDebug($"Postgres Parameter {parameter.Key}={parameter.Value}");
                }

                if (conn.Timezone != "UTC")
                {
                    Logger.LogWarning("Npgsql not using UTC time zone.");
                }

                return conn;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not open connection: " + ex.Message);
                throw;
            }
        }

        public async Task<AttribInfo> GetAttributeInfo(string equipmentTypeName, string equipmentName,
            string childEquipmentName,
            string attributeName)
        {
            try
            {
                string cmdText;
                if (string.IsNullOrEmpty(childEquipmentName))
                {
                    // Attribute of equipment
                    cmdText = $@"
                        select attr.id, attr.data_type
                            from model.attributes attr
                        join model.equipment eqpt on attr.part_of_id = eqpt.id
                        join model.equipment_types eptp on eptp.id = eqpt.type_id
                        where eptp.relative_name = '{equipmentTypeName}'
                        and eqpt.relative_name = '{equipmentName}'
                        and attr.relative_name = '{attributeName}';";
                }
                else
                {
                    // Attribute of child equipment 
                    cmdText = $@"
                        select attr.id, attr.data_type
                        from model.attributes attr
                        join model.equipment eqpt on attr.part_of_id = eqpt.id
                        and eqpt.relative_name = '{childEquipmentName}'
                        and attr.relative_name = '{attributeName}'
                        and eqpt.part_of_id =
                            (select eqpt.id from model.equipment eqpt
                                join model.equipment_types eptp on eptp.id = eqpt.type_id
                                where eptp.relative_name = '{equipmentTypeName}'
                                and eqpt.relative_name = '{equipmentName}');";
                }

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.Text };
                await cmd.PrepareAsync();

                using (var dataReader = cmd.ExecuteReader())
                {

                    if (!dataReader.Read())
                    {
                        // It is possible that the attribute has been deleted from the model but the IoT device still sends the telemetry
                        Logger.LogWarning(
                            $"Cannot find attribute {attributeName} for child equipment {childEquipmentName} under {equipmentName} of type {equipmentTypeName}");
                        return null;
                    }

                    var idObj = dataReader.GetValue(0);
                    var attribId = (long)idObj;
                    var dataTypeString = dataReader.GetValue(1).ToString();
                    DataType dataType;
                    switch (dataTypeString)
                    {
                        case "bool":
                            dataType = DataType.Bool;
                            break;
                        case "int":
                            dataType = DataType.Int;
                            break;
                        case "float":
                            dataType = DataType.Float;
                            break;
                        case "string":
                            dataType = DataType.String;
                            break;
                        case "datetime":
                            dataType = DataType.DateTime;
                            break;
                        case "interval":
                            dataType = DataType.Interval;
                            break;
                        case "object":
                            dataType = DataType.Object;
                            break;
                        case "enumeration":
                            dataType = DataType.Enumeration;
                            break;
                        case "geopoint":
                            dataType = DataType.Geopoint;
                            break;
                        default:
                            throw new InvalidDataException($"Invalid data type {dataTypeString}");
                    }

                    return new AttribInfo { Id = attribId, DataType = dataType };
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to get attribute information");
                throw;
            }
        }

        public async Task<bool> UpsertTimeSeriesArray(DataType dataType, IList<long> attributeIds, IList<object> values,
            IList<DateTime> timeStamps)
        {
            try
            {
                if (attributeIds.Count == 0)
                {
                    return true;
                }

                // Initialized array of status to 0 (Good status)
                var statuses = new long[attributeIds.Count];

                using (var conn = OpenConnection(_connectionString))
                {
                    string procedureName;
                    NpgsqlDbType parameterType;
                    switch (dataType)
                    {
                        case DataType.Bool:
                            procedureName = "history.upsert_time_series_bools_array";
                            parameterType = NpgsqlDbType.Array | NpgsqlDbType.Boolean;
                            break;

                        case DataType.Int:
                            procedureName = "history.upsert_time_series_ints_array";
                            parameterType = NpgsqlDbType.Array | NpgsqlDbType.Bigint;
                            break;

                        case DataType.Float:
                            procedureName = "history.upsert_time_series_floats_array";
                            parameterType = NpgsqlDbType.Array | NpgsqlDbType.Double;
                            values = ToDoubleList(values);
                            break;

                        case DataType.String:
                        case DataType.Enumeration:
                            procedureName = "history.upsert_time_series_strings_array";
                            parameterType = NpgsqlDbType.Array | NpgsqlDbType.Text;
                            values = ToStringList(values);
                            break;

                        case DataType.DateTime:
                            procedureName = "history.upsert_time_series_datetimes_array";
                            parameterType = NpgsqlDbType.Array | NpgsqlDbType.TimestampTz;
                            break;

                        case DataType.Object:
                            procedureName = "history.upsert_time_series_objects_array";
                            parameterType = NpgsqlDbType.Array | NpgsqlDbType.Jsonb;
                            break;

                        case DataType.Geopoint:
                            procedureName = "history.upsert_time_series_geopoints_array";
                            parameterType = NpgsqlDbType.Array | NpgsqlDbType.Point;
                            break;

                        default:
                            throw new ArgumentException("Unrecognized data type.");
                    }

                    using (var cmd = new NpgsqlCommand(procedureName, conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@p_tag_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint,
                            attributeIds.ToArray());
                        cmd.Parameters.AddWithValue("@p_values", parameterType, values.ToArray());
                        cmd.Parameters.AddWithValue("@p_statuses", NpgsqlDbType.Array | NpgsqlDbType.Bigint,
                            statuses.ToArray());
                        cmd.Parameters.AddWithValue("@p_timestamps", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz,
                            timeStamps.ToArray());
                        await cmd.PrepareAsync();

                        // The affected number of rows does not really represent what is inserted using this function.
                        var affectedRows = cmd.ExecuteNonQuery();
                        Logger.LogDebug(
                            $"Successfully upserted {values.Count} {dataType} values into history. Affected rows {affectedRows}");
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to upsert {values.Count} {dataType} values into history.");
                return false;
            }
        }

        private static IList<object> ToStringList(IList<object> values)
        {
            var results = new List<object>();

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value != null && !(value is string))
                {
                    // value could be of type long
                    value = value.ToString();
                }

                results.Add(value);
            }

            return results;
        }

        private static IList<object> ToDoubleList(IList<object> values)
        {
            var results = new List<object>();
            for (var i = 0; i < values.Count; i++)
            {
                // values[i] could be of type long
                results.Add(values[i] is double ? values[i] : Convert.ToDouble(values[i]));
            }

            return results;
        }

        public async Task<IDictionary<string, string>> FetchEquipmentTypes(string deviceTemplateIdCsv)
        {

            try
            {
                var etagByEquipmentTypeName = new Dictionary<string, string>();
                var cmdText = "select relative_name, document->> 'etag' from model.equipment_types " +
                              "where part_of_id = (select id from model.libraries where relative_name='local_library') " +
                              $"and document->> 'etag' is not null and relative_name in ({deviceTemplateIdCsv});";

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.Text };

                await cmd.PrepareAsync();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var etag = reader.GetString(1);
                    etagByEquipmentTypeName.Add(name, etag);
                }

                return etagByEquipmentTypeName;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to fetch equipment types.");
                throw;
            }
        }

        public async Task<IDictionary<string, string>> FetchEquipmentInstances(string deviceIdCsv)
        {
            try
            {
                var etagByEquipmentName = new Dictionary<string, string>();
                var cmdText =
                    $"select relative_name, document->> 'etag' from model.equipment where fqn in ({deviceIdCsv}) and document->> 'etag' is not null;";

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.Text };

                await cmd.PrepareAsync();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var etag = reader.GetString(1);
                    etagByEquipmentName.Add(name, etag);
                }

                return etagByEquipmentName;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to fetch equipment instances");
                throw;
            }
        }

        public async Task<IList<AzureAppConfig>> GetAppConfigs(string pgPwd)
        {
            IList<AzureAppConfig> appConfigs = new List<AzureAppConfig>();
            try
            {
                var cmdText = "select iot_central_subdomain, " +
                              "public.pgp_sym_decrypt(iot_central_api_token, @pwd), " +
                              "iot_central_application_id, " +
                              "public.pgp_sym_decrypt(event_hub_namespace_connection_string, @pwd), " +
                              "event_hub_name, " +
                              "public.pgp_sym_decrypt(blob_storage_connection_string, @pwd), " +
                              "blob_container_name, " +
                              "query_interval_in_seconds, " +
                              "parent_fqn " +
                              "from model.azure_connectors;";

                using var conn = OpenConnection(_connectionString);
                using var cmd = new NpgsqlCommand(cmdText, conn) { CommandType = CommandType.Text };
                cmd.Parameters.AddWithValue("@pwd", NpgsqlDbType.Text, pgPwd);
                await cmd.PrepareAsync();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var iotcSubdomain = reader.GetString(0);
                    var iotcApiToken = reader.GetString(1);
                    var iotcAppid = reader.GetString(2);
                    var eventhubNamespaceConnStr = reader.GetString(3);
                    var eventhubName = reader.GetString(4);
                    var blobStorageConnStr = reader.GetString(5);
                    var blobContainerName = reader.GetString(6);
                    var queryIntervalInSeconds = reader.GetInt32(7);
                    var parentFqn = reader.GetString(8);
                    
                    var appConfig = new AzureAppConfig
                    {
                        IoTCentralSubDomain = iotcSubdomain,
                        IoTCentralApiToken = iotcApiToken,
                        IoTCentralApplicationId = iotcAppid,
                        EventHubNamespaceConnectionString = eventhubNamespaceConnStr,
                        EventHubName = eventhubName,
                        BlobStorageConnectionString = blobStorageConnStr,
                        BlobContainerName = blobContainerName,
                        QueryIntervalInSeconds = queryIntervalInSeconds,
                        ParentFqn= parentFqn

                    };

                    appConfigs.Add(appConfig);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to fetch application configurations");
                throw;
            }

            return appConfigs;
        }
    }
}