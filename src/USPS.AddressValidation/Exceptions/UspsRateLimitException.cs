using System.Net;

namespace USPS.AddressValidation.Exceptions;

/// <summary>
/// Thrown when USPS answered <c>429 Too Many Requests</c>.
/// </summary>
public sealed class UspsRateLimitException : UspsApiException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="message">Description of the failure.</param>
    /// <param name="retryAfter">Value of the <c>Retry-After</c> header, when USPS supplied one.</param>
    /// <param name="errorCode">The <c>error.code</c> value from the USPS error payload, if present.</param>
    /// <param name="responseBody">The raw response body, for diagnostics.</param>
    public UspsRateLimitException(
        string message,
        TimeSpan? retryAfter = null,
        string? errorCode = null,
        string? responseBody = null)
        : base(message, HttpStatusCode.TooManyRequests, errorCode, responseBody)
        => RetryAfter = retryAfter;

    /// <summary>How long USPS asked the caller to wait before retrying, when it said.</summary>
    public TimeSpan? RetryAfter { get; }
}
