namespace USPS.AddressValidation.Exceptions;

/// <summary>
/// Thrown when the client is not usable as configured — a missing consumer key or secret, or an
/// invalid base address.
/// </summary>
public sealed class UspsConfigurationException : UspsException
{
    /// <summary>Initializes a new instance with a message.</summary>
    public UspsConfigurationException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public UspsConfigurationException(string message, Exception? innerException)
        : base(message, innerException) { }
}
