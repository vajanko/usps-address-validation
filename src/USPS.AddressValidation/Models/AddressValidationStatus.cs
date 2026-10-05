namespace USPS.AddressValidation.Models;

/// <summary>
/// The outcome of an address validation request.
/// </summary>
/// <remarks>
/// Values map onto the USPS <c>returnCode</c> where the API returned one; the remainder are
/// derived from the USPS error payload or produced locally before a request is made.
/// </remarks>
public enum AddressValidationStatus
{
    /// <summary>
    /// USPS matched the input to exactly one delivery point (USPS return code <c>31</c>).
    /// The standardized address is available on <see cref="AddressValidationResult.Address"/>.
    /// </summary>
    Validated = 0,

    /// <summary>
    /// USPS found the building but needs more information — typically an apartment, suite or box
    /// number — to resolve a specific delivery point (USPS return code <c>32</c>).
    /// A standardized default address is still returned.
    /// </summary>
    DefaultAddress,

    /// <summary>
    /// More than one address matched the input and no default exists (USPS return code <c>22</c>).
    /// </summary>
    MultipleMatches,

    /// <summary>No address matched the input (USPS return code <c>21</c>).</summary>
    AddressNotFound,

    /// <summary>The street address is not a valid delivery address (USPS return code <c>10</c>).</summary>
    InvalidAddress,

    /// <summary>The city is missing or invalid (USPS return code <c>13</c>).</summary>
    InvalidCity,

    /// <summary>The state code is missing or invalid (USPS return code <c>12</c>).</summary>
    InvalidState,

    /// <summary>The city and state are missing or together unverifiable.</summary>
    InvalidCityState,

    /// <summary>The ZIP Code is missing or invalid (USPS return code <c>11</c>).</summary>
    InvalidZipCode,

    /// <summary>The supplied address data was not sufficient for USPS to attempt a match.</summary>
    InsufficientInput,

    /// <summary>
    /// The input failed this library's local checks and no request was sent to USPS.
    /// See <see cref="AddressValidationResult.Errors"/> for the offending fields.
    /// </summary>
    InvalidInput,

    /// <summary>USPS responded, but the outcome could not be classified.</summary>
    Unknown,
}
