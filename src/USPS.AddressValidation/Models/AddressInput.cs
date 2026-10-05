namespace USPS.AddressValidation.Models;

/// <summary>
/// A US address as supplied by the caller, before validation.
/// </summary>
/// <remarks>
/// The USPS Addresses API requires a <see cref="StreetAddress"/> plus either
/// <see cref="City"/> and <see cref="State"/>, or a <see cref="ZipCode"/> (or all three).
/// </remarks>
public sealed record AddressInput
{
    /// <summary>Firm or business name at the address. Optional, max 50 characters.</summary>
    public string? Firm { get; init; }

    /// <summary>
    /// House number and street name, for example <c>3120 M St NW</c>.
    /// May also carry the secondary unit instead of <see cref="SecondaryAddress"/>.
    /// </summary>
    public string? StreetAddress { get; init; }

    /// <summary>Secondary unit designator and value, for example <c>APT 4B</c> or <c>STE 200</c>.</summary>
    public string? SecondaryAddress { get; init; }

    /// <summary>City name.</summary>
    public string? City { get; init; }

    /// <summary>Two-character state or territory code, for example <c>DC</c>.</summary>
    public string? State { get; init; }

    /// <summary>Urbanization code. Only meaningful for Puerto Rico addresses.</summary>
    public string? Urbanization { get; init; }

    /// <summary>Five-digit ZIP Code.</summary>
    public string? ZipCode { get; init; }

    /// <summary>Four-digit ZIP+4 add-on.</summary>
    public string? ZipPlus4 { get; init; }

    /// <summary>
    /// Creates an input from the most common set of fields.
    /// </summary>
    public static AddressInput Create(
        string streetAddress,
        string? city = null,
        string? state = null,
        string? zipCode = null,
        string? secondaryAddress = null) => new()
        {
            StreetAddress = streetAddress,
            City = city,
            State = state,
            ZipCode = zipCode,
            SecondaryAddress = secondaryAddress,
        };

    /// <summary>Returns a single-line rendering of the address, useful for logging and display.</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(Firm)) parts.Add(Firm.Trim());
        if (!string.IsNullOrWhiteSpace(StreetAddress)) parts.Add(StreetAddress.Trim());
        if (!string.IsNullOrWhiteSpace(SecondaryAddress)) parts.Add(SecondaryAddress.Trim());

        var lastLine = string.Join(" ", new[]
        {
            string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
            string.IsNullOrWhiteSpace(State) ? null : State.Trim(),
            string.IsNullOrWhiteSpace(ZipPlus4) ? ZipCode?.Trim() : $"{ZipCode?.Trim()}-{ZipPlus4.Trim()}",
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

        if (lastLine.Length > 0) parts.Add(lastLine);
        return string.Join(", ", parts);
    }
}
