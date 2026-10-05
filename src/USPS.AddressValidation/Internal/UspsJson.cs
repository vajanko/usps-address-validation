using System.Text.Json;
using System.Text.Json.Serialization;

namespace USPS.AddressValidation.Internal;

/// <summary>Shared serializer settings for USPS payloads.</summary>
internal static class UspsJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
