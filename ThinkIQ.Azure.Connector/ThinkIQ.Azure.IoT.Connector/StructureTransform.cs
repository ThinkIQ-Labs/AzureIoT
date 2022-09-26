using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ThinkIQ.Azure.Connector.Utils;
using ThinkIQ.Azure.IoT.Central.Client;

namespace ThinkIQ.Azure.IoT.Connector
{
    public class StructureTransform
    {
        #region Unit mapping

        // https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md
        public static readonly IDictionary<string, string> s_UnitMapping =
            new Dictionary<string, string>
            {
                {"acre", "acre"},
                {"ampere", "ampere"},
                {"astronomicalUnit", "astronomical_unit"},
                {"bar", "bar"},
                {"bel", "bel"},
                {"bit", "bit"},
                {"bitPerSecond", "bit_per_second"},
                {"byte", "byte"},
                {"bytePerSecond", "byte_per_second"},
                {"candela", "candela"},
                {"candelaPerSquareMetre", "candela_per_square_meter"},
                {"centimetre", "centimeter"},
                {"centimetrePerSecond", "centimeter_per_second"},
                {"centimetrePerSecondSquared", "centimeter_per_second_squared"},
                {"coulomb", "coulomb"},
                {"cubicCentimetre", "cubic_centimeter"},
                {"cubicFoot", "cubic_foot"},
                {"cubicInch", "cubic_inch"},
                {"cubicMetre", "cubic_meter"},
                {"day", "day"},
                {"decibel", "decibel"},
                {"degreeCelsius", "celsius"},
                {"degreeFahrenheit", "fahrenheit"},
                {"degreeOfArc", "degree_unit_of_angle"},
                {"degreePerSecond", "degree_per_second"},
                {"electronvolt", "electronvolt"},
                {"exbibit", "exbibit"},
                {"exbibitPerSecond", "exabit_per_second"},
                {"exbibyte", "exbibyte"},
                {"exbibytePerSecond", "exbibyte_per_second"},
                {"farad", "farad"},
                {"fluidOunce", "fluid_ounce_us"},
                {"foot", "foot"},
                {"footcandle", "lumen_per_square_foot"},
                {"gallon", "us_gallon"},
                {"gForce", "standard_acceleration_of_free_fall"},
                {"gibibit", "gibibit"},
                {"gibibitPerSecond", "gigabit_per_second"},
                {"gibibyte", "gibibyte"},
                {"gibibytePerSecond", "gigabyte_per_second"},
                {"gigahertz", "gigahertz"},
                {"gigajoule", "gigajoule"},
                {"gigawatt", "gigawatt"},
                {"gram", "gram"},
                {"gramPerCubicMetre", "gram_per_cubic_meter"},
                {"gramPerHour", "gram_per_hour"},
                {"gramPerSecond", "gram_per_second"},
                {"hectare", "hectare"},
                {"henry", "henry"},
                {"hertz", "hertz"},
                {"horsepower", "horsepower"},
                {"hour", "hour"},
                {"inch", "inch"},
                {"inchesOfMercury", "inch_of_mercury"},
                {"inchesOfWater", "inch_of_water"},
                {"joule", "joule"},
                {"kelvin", "kelvin"},
                {"kibibit", "kibibit"},
                {"kibibitPerSecond", "kilobit_per_second"},
                {"kibibyte", "kibibyte"},
                {"kibibytePerSecond", "kilobyte_per_second"},
                {"kilogram", "kilogram"},
                {"kilogramPerCubicMetre", "kilogram_per_cubic_meter"},
                {"kilogramPerHour", "kilogram_per_hour"},
                {"kilogramPerSecond", "kilogram_per_second"},
                {"kilohertz", "kilohertz"},
                {"kilojoule", "kilojoule"},
                {"kilometre", "kilometer"},
                {"kilometrePerHour", "kilometer_per_hour"},
                {"kilometrePerSecond", "kilometer_per_second"},
                {"kiloohm", "kiloohm"},
                {"kilopascal", "kilopascal"},
                {"kilovolt", "kilovolt"},
                {"kilowatt", "kilowatt"},
                {"kilowattHour", "kilowatt_hour"},
                {"kilowattHourPerYear", "kilowatt_hour_per_year"},
                {"knot", "knot"},
                {"litre", "liter"},
                {"litrePerHour", "liter_per_hour"},
                {"litrePerSecond", "liter_per_second"},
                {"lumen", "lumen"},
                {"lux", "lux"},
                {"maxwell", "maxwell"},
                {"mebibit", "mebibit"},
                {"mebibitPerSecond", "megabit_per_second"},
                {"mebibyte", "mebibyte"},
                {"mebibytePerSecond", "megabyte_per_second"},
                {"megaelectronvolt", "megaelectronvolt"},
                {"megahertz", "megahertz"},
                {"megajoule", "megajoule"},
                {"megaohm", "megaohm"},
                {"megavolt", "megavolt"},
                {"megawatt", "megawatt"},
                {"metre", "meter"},
                {"metrePerHour", "meter_per_hour"},
                {"metrePerSecond", "meter_per_second"},
                {"metrePerSecondSquared", "meter_per_second_squared"},
                {"microampere", "microampere"},
                {"microfarad", "microfarad"},
                {"microgram", "microgram"},
                {"microhenry", "microhenry"},
                {"micrometre", "micrometer"},
                {"microsecond", "microsecond"},
                {"microvolt", "microvolt"},
                {"microwatt", "microwatt"},
                {"mile", "mile"},
                {"milePerHour", "mile_per_hour_statute_mile"},
                {"milePerSecond", "mile_per_second"},
                {"milliampere", "milliampere"},
                {"millibar", "millibar"},
                {"millifarad", "millifarad"},
                {"milligram", "milligram"},
                {"millihenry", "millihenry"},
                {"millilitre", "milliliter"},
                {"millilitrePerHour", "milliliter_per_hour"},
                {"millilitrePerSecond", "milliliter_per_second"},
                {"millimetre", "millimeter"},
                {"millimetresOfMercury", "conventional_millimeter_of_mercury"},
                {"milliohm", "milliohm"},
                {"millisecond", "millisecond"},
                {"millivolt", "millivolt"},
                {"milliwatt", "milliwatt"},
                {"minute", "minute"},
                {"minuteOfArc", "minute_unit_of_angle"},
                {"nanofarad", "nanofarad"},
                {"nanometre", "nanometer"},
                {"nanosecond", "nanosecond"},
                {"nauticalMile", "nautical_mile"},
                {"newton", "newton"},
                {"newtonMetre", "newton_meter"},
                {"ohm", "ohm"},
                {"ounce", "ounce_force"},
                {"pascal", "pascal"},
                {"picofarad", "picofarad"},
                {"pound", "pound_force"},
                {"poundPerSquareInch", "pound_per_square_inch"},
                {"radian", "radian"},
                {"radianPerSecond", "radian_per_second"},
                {"radianPerSecondSquared", "radian_per_second_squared"},
                {"revolutionPerMinute", "revolution_per_minute"},
                {"revolutionPerSecond", "revolution_per_second"},
                {"second", "second"},
                {"secondOfArc", "second_unit_of_angle"},
                {"slug", "slug"},
                {"squareCentimetre", "square_centimeter"},
                {"squareFoot", "square_foot"},
                {"squareInch", "square_inch"},
                {"squareKilometre", "square_kilometer"},
                {"squareMetre", "square_meter"},
                {"squareMillimetre", "square_millimeter"},
                {"tebibit", "tebibit"},
                {"tebibitPerSecond", "terabit_per_second"},
                {"tebibyte", "tebibyte"},
                {"tebibytePerSecond", "tebibyte_per_second"},
                {"tesla", "tesla"},
                {"ton", "ton_force_us_short"},
                {"tonne", "tonne"},
                {"turn", "revolution"},
                {"unity percent", "percent"},
                {"volt", "volt"},
                {"watt", "watt"},
                {"weber", "weber"},
                {"year", "year"},
                {"yobibit", "yobibit"},
                {"yobibitPerSecond", "yobibit_per_second"},
                {"yobibyte", "yobibyte"},
                {"yobibytePerSecond", "yobibyte_per_second"},
                {"zebibit", "zebibit"},
                {"zebibitPerSecond", "zebibit_per_second"},
                {"zebibyte", "zebibyte"},
                {"zebibytePerSecond", "zebibyte_per_second"}
            };
        #endregion

        public static IList<TiqEquipmentType> ExtractModel(string libraryName, 
            JObject capabilityModel,
            out IList<TiqEnumerationType> enumerationTypes,
            out IList<TiqAttributeType> attributeTypes
        )
        {
            var childEquipmentTypes = new List<TiqEquipmentType>();
            enumerationTypes = new List<TiqEnumerationType>();
            attributeTypes = new List<TiqAttributeType>();
            var interfaces = (JArray)capabilityModel["extends"];

            if (interfaces != null)
            {
                foreach (var inter in interfaces)
                {
                    // This interface belongs to the top level model
                    ExtractInterface(libraryName, inter, enumerationTypes, attributeTypes);
                }
            }

            var contents = (JArray)capabilityModel["contents"];

            if (contents == null)
            {
                return null;
            }
                      
            foreach (var content in contents)
            {               
                if (content["@type"] is JArray contentTypes)
                {
                    if (contentTypes.Any(x => x.ToString().Equals("Property") || x.ToString().Equals("Telemetry")))
                    {
                        // Found a property of the top-level device template
                        var capability = content.ToObject<Capability>();
                        var attributeType = BuildAttributeType(libraryName, capability, enumerationTypes);
                        attributeTypes.Add(attributeType);
                    }
                    else
                    {
                        throw new InvalidDataException($"Found in valid content type {contentTypes}");
                    }
                }
                else
                {
                    var contentType = content["@type"]?.ToString().ToLowerInvariant();

                    if (contentType == "component")
                    {
                        // Found component                        
                        var componentName = content["name"].ToString().ToLowerInvariant();

                        // Note that content["displayName"] would give "Component".
                        // We need to get more specific displayName. Currently, this field can only be
                        // manually edited by modifying the DTDL directly on Azure IoT Central App like "displayName": { "en": "Trailer" }
                        var componentDisplayName = content["schema"]["displayName"]?.ToString();
                        var equipmentType = new TiqEquipmentType
                        {
                            Name = componentName,
                            DisplayName = componentDisplayName
                        };
                        equipmentType.Fqn = new[] { libraryName, componentName };

                        // This interface belongs to the component
                        // Enumeration types are global so harvest them with the same collection
                        // Attribute types of the component belong to the component only.
                        // So use a new attributeTypes collection
                        var inter = content["schema"];
                        var componentAttributeTypes = new List<TiqAttributeType>();
                        ExtractInterface(libraryName, inter, enumerationTypes, componentAttributeTypes);
                        equipmentType.AttributeTypes = componentAttributeTypes;

                        childEquipmentTypes.Add(equipmentType);
                    }
                    else if (contentType == "command")
                    {
                        // TODO: Handle command
                    }
                    else
                    {
                        // Extract content directly under capabilityModel (not nested interfaces)
                        var capability = content.ToObject<Capability>();
                        var attributeType = BuildAttributeType(libraryName, capability, enumerationTypes);
                        attributeTypes.Add(attributeType);
                    }
                }
            }

            return childEquipmentTypes;
        }

        /* Sample inter
        {
          "@id": "dtmi:thinkRefrigeratedTrucks:RefrigeratedTruck6gg;1",
          "@type": [
            "Interface",
            "NamedInterface"
          ],
          "contents": [
          ],
          "displayName": "IRefrigeratedTruck",
          "name": "RefrigeratedTruck6gg"
        }
         */
        private static void ExtractInterface(string libraryName,
            JToken inter, 
            ICollection<TiqEnumerationType> enumerationTypes,
            ICollection<TiqAttributeType> attributeTypes)
        {
            var interfaceObject = inter.ToObject<Interface>();

            // Collect enumerations and attributes
            if (interfaceObject?.Contents == null)
            {
                return;
            }

            foreach (var capability in interfaceObject.Contents)
            {
                if (capability.Schema is null)
                {
                    // TODO: Handle commands
                    continue;
                }

                var attributeType = BuildAttributeType(libraryName, capability, enumerationTypes);
                attributeTypes.Add(attributeType);
            }

            /*
            if (interfaceObject.Schemas != null)
            {
                foreach (var schema in interfaceObject.Schemas)
                {
                    // A schema of an interface contains definition of states 
                    // used in command payload.
                    // TODO: Support commands                            
            }*/
        }

        private static TiqAttributeType BuildAttributeType(string libraryName, Capability capability, ICollection<TiqEnumerationType> enumerationTypes)
        {
            // capability.Id may be absent but capability.Name should exist
            var attributeType = new TiqAttributeType
            {
                Name = capability.Name.ToLowerInvariant(),
                Description = capability.Description,
                DisplayName = capability.DisplayName
            };

            string modelType = null;

            if (capability.Schema is string)
            {
                if (capability.Schema.Equals("boolean"))
                {
                    modelType = "bool";
                }
                else if (capability.Schema.Equals("integer") || capability.Schema.Equals("long"))
                {
                    modelType = "int";
                }
                else if (capability.Schema.Equals("float") || capability.Schema.Equals("double"))
                {
                    modelType = "float";
                }
                else if (capability.Schema.Equals("string"))
                {
                    modelType = "string";
                }
                else if (capability.Schema.Equals("dateTime"))
                {
                    modelType = "dateTime";
                }
                else if (capability.Schema.Equals("duration"))
                {
                    modelType = "interval";
                }
                else if (capability.Schema.Equals("geopoint"))
                {
                    modelType = "geopoint";
                }
                else
                {
                    modelType = "object";
                }
            }
            else if (capability.Schema is JObject schema)
            {
                var type = schema["@type"] as JValue;
                if (type?.Value == null)
                {
                    throw new InvalidDataException("Type cannot be null or empty.");
                }

                if (type.Value.ToString().Equals("Enum"))
                {
                    // Handle enum type
                    if (!(schema["enumValues"] is JArray enumValues))
                    {
                        throw new InvalidDataException("Enum values should be in an array.");
                    }

                    var enumerationName = (JValue)schema["@id"];
                    // var valueSchema = (JValue) schema["valueSchema"];
                    modelType = "enumeration";

                    var enumerationValues = new List<string>();
                    var enumerationNames = new List<string>();

                    foreach (var enumValue in enumValues)
                    {
                        if (!(enumValue is JObject enumObj))
                        {
                            throw new InvalidDataException("Enum value should be an object.");
                        }

                        enumerationValues.Add(enumObj["enumValue"].ToString());
                        enumerationNames.Add(enumObj["displayName"].ToString());
                    }

                    var enumerationType = new TiqEnumerationType
                    {
                        Name = enumerationName.Value.ToString(),
                        DefaultEnumerationValues = enumerationValues.ToArray(),
                        EnumerationNames = enumerationNames.ToArray()
                    };

                    enumerationType.Fqn = new[] { libraryName, enumerationType.Name.ToLowerInvariant() };
                    enumerationTypes.Add(enumerationType);

                    attributeType.EnumerationTypeFqn = enumerationType.Fqn;
                    attributeType.DefaultEnumerationValues = enumerationType.DefaultEnumerationValues;
                }
                else
                {
                    throw new InvalidDataException($"Type {type.Value} is not handled.");
                }
            }

            // Telemetry will be stored as 'dynamic'::model.source_category_enum attribute
            // Property will stored as 'config'::model.source_category_enum attribute
            attributeType.SourceCategory = capability.Type.Contains("Telemetry") ? "dynamic" : "config";

            attributeType.DataType = modelType;

            if (capability.Unit != null)
            {
                if (s_UnitMapping.ContainsKey(capability.Unit))
                {
                    var measurement = s_UnitMapping[capability.Unit];
                    attributeType.MeasurementUnitFqn = new[] { "thinkiq_base_library", measurement };
                }
                else
                {
                    throw new ApplicationException($"Unmapped unit {capability.Unit}");
                }
            }

            return attributeType;
        }
    }
}