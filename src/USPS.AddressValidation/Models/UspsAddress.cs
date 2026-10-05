using System.Text.Json.Serialization;

namespace USPS.AddressValidation.Models;

/// <summary>
/// A standardized address as returned by the USPS Addresses API.
/// </summary>
public sealed record UspsAddress
{
    /// <summary>Firm or business name at the address.</summary>
    [JsonPropertyName("firm")]
    public string? Firm { get; init; }

    /// <summary>Standardized primary address line. May include the secondary unit.</summary>
    [JsonPropertyName("streetAddress")]
    public string? StreetAddress { get; init; }

    /// <summary>Abbreviated form of <see cref="StreetAddress"/>.</summary>
    [JsonPropertyName("streetAddressAbbreviation")]
    public string? StreetAddressAbbreviation { get; init; }

    /// <summary>Standardized secondary unit designator and value.</summary>
    [JsonPropertyName("secondaryAddress")]
    public string? SecondaryAddress { get; init; }

    /// <summary>Standardized city name.</summary>
    [JsonPropertyName("city")]
    public string? City { get; init; }

    /// <summary>Abbreviated city name.</summary>
    [JsonPropertyName("cityAbbreviation")]
    public string? CityAbbreviation { get; init; }

    /// <summary>Two-character state or territory code.</summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>Five-digit ZIP Code.</summary>
    [JsonPropertyName("ZIPCode")]
    public string? ZipCode { get; init; }

    /// <summary>Four-digit ZIP+4 add-on identifying the delivery point.</summary>
    [JsonPropertyName("ZIPPlus4")]
    public string? ZipPlus4 { get; init; }

    /// <summary>Urbanization, for Puerto Rico addresses.</summary>
    [JsonPropertyName("urbanization")]
    public string? Urbanization { get; init; }

    /// <summary>City / state / ZIP line as USPS formats it.</summary>
    [JsonPropertyName("lastline")]
    public string? LastLine { get; init; }

    /// <summary>City / state / ZIP line using the abbreviated city name.</summary>
    [JsonPropertyName("lastlineAbbr")]
    public string? LastLineAbbreviated { get; init; }

    /// <summary>The full ZIP+4 in <c>12345-6789</c> form, or the 5-digit ZIP when no add-on was returned.</summary>
    [JsonIgnore]
    public string? FullZipCode => string.IsNullOrWhiteSpace(ZipCode)
        ? null
        : string.IsNullOrWhiteSpace(ZipPlus4) ? ZipCode : $"{ZipCode}-{ZipPlus4}";

    /// <summary>Renders the address as the lines that would be printed on a mail piece.</summary>
    public IReadOnlyList<string> ToAddressLines()
    {
        var lines = new List<string>(5);
        if (!string.IsNullOrWhiteSpace(Firm)) lines.Add(Firm);
        if (!string.IsNullOrWhiteSpace(Urbanization)) lines.Add(Urbanization);
        if (!string.IsNullOrWhiteSpace(StreetAddress)) lines.Add(StreetAddress);
        if (!string.IsNullOrWhiteSpace(SecondaryAddress)) lines.Add(SecondaryAddress);

        var lastLine = LastLine;
        if (string.IsNullOrWhiteSpace(lastLine))
        {
            lastLine = string.Join(" ", new[] { City, State, FullZipCode }
                .Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        if (!string.IsNullOrWhiteSpace(lastLine)) lines.Add(lastLine);
        return lines;
    }

    /// <summary>Renders the address on a single line.</summary>
    public override string ToString() => string.Join(", ", ToAddressLines());
}
