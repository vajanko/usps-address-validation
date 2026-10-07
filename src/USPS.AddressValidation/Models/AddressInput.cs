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

    /// <summary>
    /// The ZIP Code, either five digits (<c>20007</c>) or the full ZIP+4 (<c>20007-3704</c>).
    /// </summary>
    /// <remarks>
    /// The USPS API takes the two parts as separate parameters; the client splits this value on
    /// the way out, so callers never have to.
    /// </remarks>
    public string? ZipCode { get; init; }

    /// <summary>
    /// Creates an input from the most common set of fields.
    /// </summary>
    /// <param name="streetAddress">House number and street name.</param>
    /// <param name="city">City name.</param>
    /// <param name="state">Two-character state code.</param>
    /// <param name="zipCode">ZIP Code, as <c>20007</c> or <c>20007-3704</c>.</param>
    /// <param name="secondaryAddress">Secondary unit designator and value.</param>
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
            string.IsNullOrWhiteSpace(ZipCode) ? null : ZipCode.Trim(),
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

        if (lastLine.Length > 0) parts.Add(lastLine);
        return string.Join(", ", parts);
    }
}
