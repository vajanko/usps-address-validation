using System.Text.Json;
using System.Text.Json.Serialization;

namespace USPS.AddressValidation.Models;

/// <summary>
/// Delivery-point metadata USPS returns alongside a standardized address.
/// </summary>
/// <remarks>
/// The USPS <c>addressAdditionalInfo</c> object carries well over fifty fields. The ones most
/// callers act on are surfaced as properties; everything else is preserved verbatim in
/// <see cref="Extensions"/> so no information from the response is lost.
/// </remarks>
public sealed record AddressAdditionalInfo
{
    /// <summary>Two-digit delivery point code that, with ZIP+4, uniquely identifies a delivery address.</summary>
    [JsonPropertyName("deliveryPoint")]
    public string? DeliveryPoint { get; init; }

    /// <summary>Carrier route code.</summary>
    [JsonPropertyName("carrierRoute")]
    public string? CarrierRoute { get; init; }

    /// <summary>County containing the delivery point.</summary>
    [JsonPropertyName("countyName")]
    public string? CountyName { get; init; }

    /// <summary>Congressional district number, or <c>AL</c> for at-large.</summary>
    [JsonPropertyName("congressDistrict")]
    public string? CongressDistrict { get; init; }

    /// <summary>USPS address footnotes.</summary>
    [JsonPropertyName("footnotes")]
    public string? Footnotes { get; init; }

    /// <summary>
    /// ZIP+4 record type: <c>G</c> general delivery, <c>P</c> PO Box, <c>R</c> rural route,
    /// <c>H</c> highrise, <c>F</c> firm, <c>S</c> street.
    /// </summary>
    [JsonPropertyName("recordType")]
    public string? RecordType { get; init; }

    /// <summary>Secondary information extracted from the street address line.</summary>
    [JsonPropertyName("secondaryInfo")]
    public string? SecondaryInfo { get; init; }

    /// <summary>
    /// USPS match return code: <c>31</c> single match, <c>32</c> default match, <c>22</c> multiple,
    /// <c>21</c> not found, <c>13</c> invalid city, <c>12</c> invalid state, <c>11</c> invalid ZIP,
    /// <c>10</c> invalid address.
    /// </summary>
    [JsonPropertyName("returnCode")]
    public int? ReturnCode { get; init; }

    /// <summary>Description of <see cref="ReturnCode"/>.</summary>
    [JsonPropertyName("returnCodeText")]
    public string? ReturnCodeText { get; init; }

    /// <summary>
    /// Delivery Point Validation result: <c>Y</c> primary and secondary confirmed, <c>D</c> primary
    /// confirmed with secondary missing, <c>S</c> primary confirmed with secondary unconfirmed,
    /// <c>N</c> not confirmed.
    /// </summary>
    [JsonPropertyName("DPVConfirmation")]
    public string? DpvConfirmation { get; init; }

    /// <summary>Enhanced DPV confirmation, which also reports whether USPS delivers to the address.</summary>
    [JsonPropertyName("DPVEnhancedConfirmation")]
    public string? DpvEnhancedConfirmation { get; init; }

    /// <summary>DPV footnote codes.</summary>
    [JsonPropertyName("DPVFootnotes")]
    public string? DpvFootnotes { get; init; }

    /// <summary><c>Y</c> when the address is a business address.</summary>
    [JsonPropertyName("DPVBusiness")]
    public string? DpvBusiness { get; init; }

    /// <summary><c>Y</c> when the address is a Commercial Mail Receiving Agency.</summary>
    [JsonPropertyName("DPVCMRA")]
    public string? DpvCmra { get; init; }

    /// <summary><c>Y</c> when the location is vacant.</summary>
    [JsonPropertyName("DPVVacant")]
    public string? DpvVacant { get; init; }

    /// <summary><c>Y</c> when the address does not receive delivery and is not counted as a possible delivery.</summary>
    [JsonPropertyName("DPVNostat")]
    public string? DpvNoStat { get; init; }

    /// <summary><c>Y</c> when the returned ZIP Code corresponds to the returned city and state.</summary>
    [JsonPropertyName("ZIP5Valid")]
    public string? Zip5Valid { get; init; }

    /// <summary><c>Y</c> when the ZIP Code facility serves PO Box addresses only.</summary>
    [JsonPropertyName("POBoxOnlyZIP")]
    public string? PoBoxOnlyZip { get; init; }

    /// <summary><c>Y</c> when the matched address is a default rather than a specific delivery point.</summary>
    [JsonPropertyName("defaultFlag")]
    public string? DefaultFlag { get; init; }

    /// <summary>Every other field USPS returned, keyed by its JSON property name.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> Extensions { get; init; } = new Dictionary<string, JsonElement>();

    /// <summary>
    /// <see langword="true"/> when DPV confirmed both the primary and, where present, the secondary number.
    /// </summary>
    [JsonIgnore]
    public bool IsDeliveryPointConfirmed =>
        string.Equals(DpvConfirmation, "Y", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <see langword="true"/> when DPV confirmed the building but reported the secondary unit as
    /// missing (<c>D</c>) or unconfirmed (<c>S</c>).
    /// </summary>
    [JsonIgnore]
    public bool IsSecondaryUnitProblem =>
        string.Equals(DpvConfirmation, "D", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(DpvConfirmation, "S", StringComparison.OrdinalIgnoreCase);

    /// <summary><see langword="true"/> when USPS flagged the address as a business.</summary>
    [JsonIgnore]
    public bool IsBusiness => string.Equals(DpvBusiness, "Y", StringComparison.OrdinalIgnoreCase);

    /// <summary><see langword="true"/> when USPS flagged the address as vacant.</summary>
    [JsonIgnore]
    public bool IsVacant => string.Equals(DpvVacant, "Y", StringComparison.OrdinalIgnoreCase);
}
