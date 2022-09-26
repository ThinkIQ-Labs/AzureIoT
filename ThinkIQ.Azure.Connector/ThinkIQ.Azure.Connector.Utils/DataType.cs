
namespace ThinkIQ.Azure.Connector.Utils
{
    // Matching database enum
    //CREATE TYPE model.scalar_type_enum AS ENUM
    //    ('bool', 'int', 'float', 'string', 'datetime', 'interval', 'object', 'enumeration', 'geopoint');
    public enum DataType
    {
        Bool,
        Int,
        Float,
        String,
        DateTime,
        Interval,
        Object,
        Enumeration,
        Geopoint
    }
}
