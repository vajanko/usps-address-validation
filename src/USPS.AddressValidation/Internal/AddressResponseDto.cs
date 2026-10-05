using System.Text.Json.Serialization;
using USPS.AddressValidation.Models;

namespace USPS.AddressValidation.Internal;

/// <summary>Wire shape of a successful <c>GET /addresses/v3/address</c> response.</summary>
internal sealed class AddressResponseDto
{
    [JsonPropertyName("firm")]
    public string? Firm { get; init; }

    [JsonPropertyName("address")]
    public UspsAddress? Address { get; init; }

    [JsonPropertyName("additionalInfo")]
    public AddressAdditionalInfo? AdditionalInfo { get; init; }

    [JsonPropertyName("corrections")]
    public List<AddressCode>? Corrections { get; init; }

    [JsonPropertyName("matches")]
    public List<AddressCode>? Matches { get; init; }

    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; init; }
}
