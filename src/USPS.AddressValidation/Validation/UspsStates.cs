namespace USPS.AddressValidation.Validation;

/// <summary>
/// The two-character state, territory and military codes the USPS Addresses API accepts.
/// </summary>
public static class UspsStates
{
    private static readonly HashSet<string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Armed forces.
        "AA", "AE", "AP",
        // States.
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "DC", "FL", "GA", "HI", "ID", "IL", "IN",
        "IA", "KS", "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH",
        "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT",
        "VT", "VA", "WA", "WV", "WI", "WY",
        // Territories and freely associated states.
        "AS", "FM", "GU", "MH", "MP", "PR", "PW", "VI",
    };

    /// <summary>All accepted codes, ordered alphabetically.</summary>
    public static IReadOnlyCollection<string> All { get; } = [.. Codes.Order(StringComparer.Ordinal)];

    /// <summary>Returns whether <paramref name="code"/> is an accepted two-character code.</summary>
    public static bool IsValid(string? code) => !string.IsNullOrWhiteSpace(code) && Codes.Contains(code.Trim());
}
