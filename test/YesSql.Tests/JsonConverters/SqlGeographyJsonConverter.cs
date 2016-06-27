using Newtonsoft.Json;
using System;

#if NET451
using Microsoft.SqlServer.Types;
#endif

namespace YesSql.Tests.JsonConverters
{
#if NET451
    public class SqlGeographyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsAssignableFrom(typeof(string));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var geo = serializer.Deserialize<string>(reader);
            return SqlGeography.Parse(geo);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string geo = new string(((SqlGeography)value).STAsText().Value);
            serializer.Serialize(writer, geo);
        }
    }
#endif
}
