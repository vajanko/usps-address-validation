using System.Text.RegularExpressions;

namespace USPS.AddressValidation.Internal;

/// <summary>
/// Parses the single ZIP Code value callers supply into the two parts the USPS Addresses API
/// expects as separate query parameters.
/// </summary>
internal static partial class ZipCodeParser
{
    /// <summary>Matches <c>00000</c> and <c>00000-0000</c>, capturing each part.</summary>
    [GeneratedRegex(@"^(?<zip5>\d{5})(?:-(?<plus4>\d{4}))?$")]
    private static partial Regex Pattern { get; }

    /// <summary>
    /// Returns whether <paramref name="zipCode"/> is a well-formed ZIP Code — five digits, with an
    /// optional four-digit add-on separated by a hyphen. Surrounding whitespace is ignored.
    /// </summary>
    public static bool IsValid(string? zipCode) =>
        !string.IsNullOrWhiteSpace(zipCode) && Pattern.IsMatch(zipCode.Trim());

    /// <summary>
    /// Splits <paramref name="zipCode"/> into its five-digit and add-on parts. Both are
    /// <see langword="null"/> when the value is absent or malformed; <c>Plus4</c> alone is
    /// <see langword="null"/> when no add-on was supplied.
    /// </summary>
    public static (string? Zip5, string? Plus4) Split(string? zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return (null, null);
        }

        var match = Pattern.Match(zipCode.Trim());
        if (!match.Success)
        {
            return (null, null);
        }

        var plus4 = match.Groups["plus4"];
        return (match.Groups["zip5"].Value, plus4.Success ? plus4.Value : null);
    }
}
