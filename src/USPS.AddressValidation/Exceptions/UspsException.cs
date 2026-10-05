namespace USPS.AddressValidation.Exceptions;

/// <summary>
/// Base type for every failure of a USPS API call. Thrown when the request itself could not be
/// completed; an address that USPS simply could not validate is reported as a result, not as an
/// exception.
/// </summary>
public class UspsException : Exception
{
    /// <summary>Initializes a new instance with a message.</summary>
    public UspsException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public UspsException(string message, Exception? innerException) : base(message, innerException) { }
}
