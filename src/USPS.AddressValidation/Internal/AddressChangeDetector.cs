using USPS.AddressValidation.Models;

namespace USPS.AddressValidation.Internal;

/// <summary>
/// Compares the address the caller submitted with the one USPS standardized, so callers can see
/// exactly what changed — a completed ZIP+4, an abbreviated street type, a corrected city.
/// </summary>
internal static class AddressChangeDetector
{
    public static IReadOnlyList<AddressFieldChange> Detect(AddressInput input, UspsAddress? standardized)
    {
        if (standardized is null)
        {
            return [];
        }

        var changes = new List<AddressFieldChange>();

        Compare(changes, nameof(UspsAddress.Firm), input.Firm, standardized.Firm);
        Compare(changes, nameof(UspsAddress.StreetAddress), input.StreetAddress, standardized.StreetAddress);
        Compare(changes, nameof(UspsAddress.SecondaryAddress), input.SecondaryAddress, standardized.SecondaryAddress);
        Compare(changes, nameof(UspsAddress.City), input.City, standardized.City);
        Compare(changes, nameof(UspsAddress.State), input.State, standardized.State);
        // The caller supplies one ZIP Code value; USPS answers with the two halves separately, so
        // split the input to report each half on its own - "ZIP+4 completed" is the useful signal.
        var (zip5, zipPlus4) = ZipCodeParser.Split(input.ZipCode);
        Compare(changes, nameof(UspsAddress.ZipCode), zip5, standardized.ZipCode);
        Compare(changes, nameof(UspsAddress.ZipPlus4), zipPlus4, standardized.ZipPlus4);
        Compare(changes, nameof(UspsAddress.Urbanization), input.Urbanization, standardized.Urbanization);

        return changes;
    }

    private static void Compare(List<AddressFieldChange> changes, string field, string? original, string? standardized)
    {
        var left = Normalize(original);
        var right = Normalize(standardized);

        if (right is null)
        {
            // USPS dropped or did not return the field; not a correction worth reporting.
            return;
        }

        if (left is null)
        {
            changes.Add(new AddressFieldChange(field, null, standardized, AddressChangeKind.Completed));
            return;
        }

        if (!string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(new AddressFieldChange(field, original, standardized, AddressChangeKind.Corrected));
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
