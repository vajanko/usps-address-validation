using USPS.AddressValidation.Internal;
using USPS.AddressValidation.Models;

namespace USPS.AddressValidation.Validation;

/// <summary>
/// Checks an <see cref="AddressInput"/> against the constraints the USPS Addresses API documents,
/// so obviously unusable input is rejected without spending a call.
/// </summary>
public static class AddressInputValidator
{
    private const int MaxFirmLength = 50;
    private const int MaxStreetAddressLength = 50;
    private const int MaxSecondaryAddressLength = 50;
    private const int MaxCityLength = 28;
    private const int MaxUrbanizationLength = 96;

    /// <summary>
    /// Validates <paramref name="input"/> and returns every problem found. An empty list means the
    /// input is well-formed enough to send.
    /// </summary>
    public static IReadOnlyList<AddressValidationError> Validate(AddressInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<AddressValidationError>();

        if (string.IsNullOrWhiteSpace(input.StreetAddress))
        {
            errors.Add(Error("A street address is required.", nameof(AddressInput.StreetAddress)));
        }
        else if (input.StreetAddress.Trim().Length > MaxStreetAddressLength)
        {
            errors.Add(Error(
                $"The street address must be at most {MaxStreetAddressLength} characters.",
                nameof(AddressInput.StreetAddress)));
        }

        var hasCity = !string.IsNullOrWhiteSpace(input.City);
        var hasState = !string.IsNullOrWhiteSpace(input.State);
        var hasZip = !string.IsNullOrWhiteSpace(input.ZipCode);

        if (!hasZip && !(hasCity && hasState))
        {
            errors.Add(Error(
                "Either a city and state, or a ZIP Code, must be supplied.",
                nameof(AddressInput.ZipCode)));
        }

        if (hasCity && input.City!.Trim().Length > MaxCityLength)
        {
            errors.Add(Error($"The city must be at most {MaxCityLength} characters.", nameof(AddressInput.City)));
        }

        if (hasState && !UspsStates.IsValid(input.State))
        {
            errors.Add(Error(
                $"'{input.State}' is not a valid two-character US state or territory code.",
                nameof(AddressInput.State)));
        }

        if (hasZip && !ZipCodeParser.IsValid(input.ZipCode))
        {
            errors.Add(Error(
                $"'{input.ZipCode}' is not a valid ZIP Code. Expected 00000 or 00000-0000.",
                nameof(AddressInput.ZipCode)));
        }

        if (!string.IsNullOrWhiteSpace(input.Firm) && input.Firm.Trim().Length > MaxFirmLength)
        {
            errors.Add(Error($"The firm must be at most {MaxFirmLength} characters.", nameof(AddressInput.Firm)));
        }

        if (!string.IsNullOrWhiteSpace(input.SecondaryAddress) &&
            input.SecondaryAddress.Trim().Length > MaxSecondaryAddressLength)
        {
            errors.Add(Error(
                $"The secondary address must be at most {MaxSecondaryAddressLength} characters.",
                nameof(AddressInput.SecondaryAddress)));
        }

        if (!string.IsNullOrWhiteSpace(input.Urbanization) &&
            input.Urbanization.Trim().Length > MaxUrbanizationLength)
        {
            errors.Add(Error(
                $"The urbanization must be at most {MaxUrbanizationLength} characters.",
                nameof(AddressInput.Urbanization)));
        }

        return errors;
    }

    private static AddressValidationError Error(string message, string field) =>
        new(message, ValidationErrorSource.Local, Code: null, Field: field);
}
