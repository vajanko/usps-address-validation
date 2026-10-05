namespace USPS.AddressValidation.Exceptions;

/// <summary>
/// Thrown when USPS answered successfully but the body could not be understood as the documented
/// response shape.
/// </summary>
public sealed class UspsSerializationException : UspsException
{
    /// <summary>Initializes a new instance.</summary>
    public UspsSerializationException(string message, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException) => ResponseBody = responseBody;

    /// <summary>The raw response body, for diagnostics.</summary>
    public string? ResponseBody { get; }
}
