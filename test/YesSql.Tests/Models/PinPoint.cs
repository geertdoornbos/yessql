using Newtonsoft.Json;
using System;
using YesSql.Tests.JsonConverters;

#if NET451
using Microsoft.SqlServer.Types;
#endif

namespace YesSql.Tests.Models
{
    public class PinPoint
    {
        public string Name { get; set; }

#if NET451
        [JsonConverter(typeof(SqlGeographyJsonConverter))]
        public SqlGeography Location { get; set; }
#endif
    }
}
