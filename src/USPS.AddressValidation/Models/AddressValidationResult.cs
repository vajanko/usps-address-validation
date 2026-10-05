namespace USPS.AddressValidation.Models;

/// <summary>
/// The outcome of validating one address against the USPS Addresses API.
/// </summary>
/// <remarks>
/// A result is always returned for an answer USPS was able to give — including "this address does
/// not exist". Failures of the call itself (unreachable service, missing credentials, throttling,
/// server faults) surface as an exception derived from <see cref="Exceptions.UspsException"/>
/// rather than as a result.
/// </remarks>
public sealed record AddressValidationResult
{
    private AddressValidationResult(AddressValidationStatus status) => Status = status;

    /// <summary>Classification of the outcome.</summary>
    public AddressValidationStatus Status { get; private init; }

    /// <summary>
    /// The standardized address, when USPS returned one. Populated for
    /// <see cref="AddressValidationStatus.Validated"/> and normally also for
    /// <see cref="AddressValidationStatus.DefaultAddress"/>; <see langword="null"/> otherwise.
    /// </summary>
    public UspsAddress? Address { get; private init; }

    /// <summary>Delivery-point metadata that accompanied the address.</summary>
    public AddressAdditionalInfo? AdditionalInfo { get; private init; }

    /// <summary>
    /// Codes describing how the input would need to change to get a better match — for example
    /// <c>32</c> ("more information is needed") or <c>22</c> ("multiple addresses were found").
    /// </summary>
    public IReadOnlyList<AddressCode> Corrections { get; private init; } = [];

    /// <summary>Codes describing the quality of the match, for example <c>31</c> ("exact match").</summary>
    public IReadOnlyList<AddressCode> Matches { get; private init; } = [];

    /// <summary>Free-text warnings USPS attached to the response.</summary>
    public IReadOnlyList<string> Warnings { get; private init; } = [];

    /// <summary>Reasons the address could not be validated. Empty when <see cref="IsValid"/> is true.</summary>
    public IReadOnlyList<AddressValidationError> Errors { get; private init; } = [];

    /// <summary>
    /// Field-by-field differences between the submitted address and the standardized address,
    /// such as a ZIP Code that was empty on input and completed by USPS.
    /// </summary>
    public IReadOnlyList<AddressFieldChange> Changes { get; private init; } = [];

    /// <summary>The address as submitted, echoed back for convenience.</summary>
    public AddressInput? Input { get; private init; }

    /// <summary>
    /// <see langword="true"/> only when USPS matched the input to a single delivery point.
    /// A <see cref="AddressValidationStatus.DefaultAddress"/> result is deliberately not "valid":
    /// the building exists but the address is incomplete.
    /// </summary>
    public bool IsValid => Status == AddressValidationStatus.Validated;

    /// <summary><see langword="true"/> when a standardized address is available to read.</summary>
    public bool HasAddress => Address is not null;

    /// <summary><see langword="true"/> when USPS standardized or completed at least one field.</summary>
    public bool WasModified => Changes.Count > 0;

    /// <summary>The first error message, or <see langword="null"/> when there are no errors.</summary>
    public string? ErrorMessage => Errors.Count > 0 ? Errors[0].Message : null;

    /// <summary>Builds a successful (or partially successful) result carrying a standardized address.</summary>
    internal static AddressValidationResult FromUsps(
        AddressValidationStatus status,
        AddressInput input,
        UspsAddress? address,
        AddressAdditionalInfo? additionalInfo,
        IReadOnlyList<AddressCode> corrections,
        IReadOnlyList<AddressCode> matches,
        IReadOnlyList<string> warnings,
        IReadOnlyList<AddressValidationError> errors,
        IReadOnlyList<AddressFieldChange> changes) => new(status)
        {
            Input = input,
            Address = address,
            AdditionalInfo = additionalInfo,
            Corrections = corrections,
            Matches = matches,
            Warnings = warnings,
            Errors = errors,
            Changes = changes,
        };

    /// <summary>Builds a result for input this library rejected before contacting USPS.</summary>
    internal static AddressValidationResult FromLocalValidation(
        AddressInput input,
        IReadOnlyList<AddressValidationError> errors) => new(AddressValidationStatus.InvalidInput)
        {
            Input = input,
            Errors = errors,
        };

    /// <inheritdoc />
    public override string ToString() => Status switch
    {
        AddressValidationStatus.Validated => $"{Status}: {Address}",
        _ when Address is not null => $"{Status}: {Address} ({ErrorMessage ?? "see corrections"})",
        _ => $"{Status}: {ErrorMessage ?? "no address returned"}",
    };
}
