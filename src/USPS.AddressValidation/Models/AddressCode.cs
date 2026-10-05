using System.Text.Json.Serialization;

namespace USPS.AddressValidation.Models;

/// <summary>
/// A USPS code/description pair, used for both address matches and address corrections.
/// </summary>
/// <param name="Code">The USPS code, for example <c>31</c> (exact match) or <c>32</c> (default match).</param>
/// <param name="Text">The human-readable description USPS supplied for the code.</param>
public sealed record AddressCode(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("text")] string? Text)
{
    /// <inheritdoc />
    public override string ToString() => string.IsNullOrWhiteSpace(Code) ? Text ?? string.Empty : $"{Code}: {Text}";
}
