using System.Net;

namespace USPS.AddressValidation.Exceptions;

/// <summary>
/// Thrown when USPS rejected the credentials: the OAuth token could not be obtained, or the
/// Addresses API answered <c>401</c>/<c>403</c> even after the token was refreshed.
/// </summary>
public sealed class UspsAuthenticationException : UspsApiException
{
    /// <summary>Initializes a new instance.</summary>
    public UspsAuthenticationException(
        string message,
        HttpStatusCode statusCode,
        string? errorCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, statusCode, errorCode, responseBody, innerException) { }
}
