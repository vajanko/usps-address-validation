using USPS.AddressValidation.Models;

namespace USPS.AddressValidation.Internal;

/// <summary>
/// Turns the several ways USPS signals an outcome — a numeric <c>returnCode</c>, match and
/// correction codes, or the message on an error envelope — into a single
/// <see cref="AddressValidationStatus"/>.
/// </summary>
internal static class UspsStatusMapper
{
    /// <summary>USPS match return codes, from the Addresses 3.0 specification.</summary>
    private const int SingleMatch = 31;
    private const int DefaultMatch = 32;
    private const int MultipleMatch = 22;
    private const int NotFound = 21;
    private const int InvalidCity = 13;
    private const int InvalidState = 12;
    private const int InvalidZip = 11;
    private const int InvalidAddress = 10;

    /// <summary>
    /// Classifies a <c>200 OK</c> response. Prefers the explicit <c>returnCode</c>, then the match
    /// and correction codes, and finally falls back to whether an address came back at all.
    /// </summary>
    public static AddressValidationStatus FromSuccessResponse(AddressResponseDto response)
    {
        if (response.AdditionalInfo?.ReturnCode is { } returnCode)
        {
            var mapped = FromReturnCode(returnCode);
            if (mapped != AddressValidationStatus.Unknown)
            {
                return mapped;
            }
        }

        if (HasCode(response.Matches, SingleMatch))
        {
            return AddressValidationStatus.Validated;
        }

        if (HasCode(response.Corrections, DefaultMatch) || HasCode(response.Matches, DefaultMatch))
        {
            return AddressValidationStatus.DefaultAddress;
        }

        if (HasCode(response.Corrections, MultipleMatch) || HasCode(response.Matches, MultipleMatch))
        {
            return AddressValidationStatus.MultipleMatches;
        }

        if (response.Address is null || string.IsNullOrWhiteSpace(response.Address.StreetAddress))
        {
            return AddressValidationStatus.Unknown;
        }

        // USPS returned a standardized address with no qualifying codes. A ZIP+4 is only ever
        // assigned to a resolved delivery point, so treat its presence as a match and its absence
        // as an address that needs more information.
        return string.IsNullOrWhiteSpace(response.Address.ZipPlus4)
            ? AddressValidationStatus.DefaultAddress
            : AddressValidationStatus.Validated;
    }

    /// <summary>Maps a USPS numeric return code.</summary>
    public static AddressValidationStatus FromReturnCode(int returnCode) => returnCode switch
    {
        SingleMatch => AddressValidationStatus.Validated,
        DefaultMatch => AddressValidationStatus.DefaultAddress,
        MultipleMatch => AddressValidationStatus.MultipleMatches,
        NotFound => AddressValidationStatus.AddressNotFound,
        InvalidCity => AddressValidationStatus.InvalidCity,
        InvalidState => AddressValidationStatus.InvalidState,
        InvalidZip => AddressValidationStatus.InvalidZipCode,
        InvalidAddress => AddressValidationStatus.InvalidAddress,
        _ => AddressValidationStatus.Unknown,
    };

    /// <summary>
    /// Classifies a <c>400</c> or <c>404</c> error envelope. USPS distinguishes these cases only in
    /// the message text, so the documented messages are matched on their distinguishing phrases.
    /// </summary>
    public static AddressValidationStatus FromErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return AddressValidationStatus.AddressNotFound;
        }

        var text = message.Trim();

        if (Contains(text, "more than one address")) return AddressValidationStatus.MultipleMatches;
        if (Contains(text, "city and state")) return AddressValidationStatus.InvalidCityState;
        if (Contains(text, "insufficient")) return AddressValidationStatus.InsufficientInput;
        if (Contains(text, "invalid delivery address")) return AddressValidationStatus.InvalidAddress;
        if (Contains(text, "state code")) return AddressValidationStatus.InvalidState;
        if (Contains(text, "zip code")) return AddressValidationStatus.InvalidZipCode;
        if (Contains(text, "city")) return AddressValidationStatus.InvalidCity;
        if (Contains(text, "no match") || Contains(text, "not found")) return AddressValidationStatus.AddressNotFound;

        return AddressValidationStatus.AddressNotFound;
    }

    private static bool Contains(string text, string term) =>
        text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool HasCode(IReadOnlyList<AddressCode>? codes, int code)
    {
        if (codes is null)
        {
            return false;
        }

        var wanted = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < codes.Count; i++)
        {
            if (string.Equals(codes[i].Code?.Trim(), wanted, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
