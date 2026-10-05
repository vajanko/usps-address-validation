namespace USPS.AddressValidation.Exceptions;

/// <summary>
/// Thrown when the USPS service could not be reached at all — DNS or TLS failure, connection
/// refused, or the request timing out.
/// </summary>
public sealed class UspsTransportException : UspsException
{
    /// <summary>Initializes a new instance.</summary>
    public UspsTransportException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
