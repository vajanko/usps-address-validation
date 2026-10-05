using System.Net;

namespace USPS.AddressValidation.Exceptions;

/// <summary>
/// Thrown when USPS answered with a status code that indicates the request failed rather than the
/// address being unverifiable — a server fault, for example.
/// </summary>
public class UspsApiException : UspsException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="message">Description of the failure.</param>
    /// <param name="statusCode">The HTTP status code USPS returned.</param>
    /// <param name="errorCode">The <c>error.code</c> value from the USPS error payload, if present.</param>
    /// <param name="responseBody">The raw response body, truncated for diagnostics.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public UspsApiException(
        string message,
        HttpStatusCode statusCode,
        string? errorCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ResponseBody = responseBody;
    }

    /// <summary>The HTTP status code USPS returned.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The USPS <c>error.code</c> value, when the response carried one.</summary>
    public string? ErrorCode { get; }

    /// <summary>The raw response body, for diagnostics.</summary>
    public string? ResponseBody { get; }
}
