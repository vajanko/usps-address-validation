namespace USPS.AddressValidation.Models;

/// <summary>
/// Where a validation error was produced.
/// </summary>
public enum ValidationErrorSource
{
    /// <summary>Produced by this library before the request was sent.</summary>
    Local = 0,

    /// <summary>Reported by the USPS API.</summary>
    Usps,
}

/// <summary>
/// A single reason an address could not be validated.
/// </summary>
/// <param name="Message">Human-readable description of the problem.</param>
/// <param name="Source">Whether the error came from local checks or from USPS.</param>
/// <param name="Code">The USPS error code, where one was supplied.</param>
/// <param name="Field">The offending input field, where it could be identified.</param>
public sealed record AddressValidationError(
    string Message,
    ValidationErrorSource Source = ValidationErrorSource.Local,
    string? Code = null,
    string? Field = null)
{
    /// <inheritdoc />
    public override string ToString() => Field is null ? Message : $"{Field}: {Message}";
}
