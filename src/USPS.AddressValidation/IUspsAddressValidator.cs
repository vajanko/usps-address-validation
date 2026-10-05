using USPS.AddressValidation.Models;

namespace USPS.AddressValidation;

/// <summary>
/// Validates and standardizes US addresses against the USPS Addresses 3.0 API.
/// </summary>
/// <remarks>
/// <para>
/// Every answer USPS is able to give — a validated address, "no such address", "more than one
/// address matched" — comes back as an <see cref="AddressValidationResult"/>. Only a failure of the
/// call itself throws, and always as an exception derived from
/// <see cref="Exceptions.UspsException"/>.
/// </para>
/// </remarks>
public interface IUspsAddressValidator
{
    /// <summary>
    /// Validates one address.
    /// </summary>
    /// <param name="address">The address to validate.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>
    /// The outcome, including the standardized address when USPS produced one and the list of
    /// fields USPS corrected or completed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    /// <exception cref="Exceptions.UspsConfigurationException">A credential is missing or the configuration is unusable.</exception>
    /// <exception cref="Exceptions.UspsAuthenticationException">USPS rejected the credentials.</exception>
    /// <exception cref="Exceptions.UspsRateLimitException">USPS throttled the request.</exception>
    /// <exception cref="Exceptions.UspsApiException">USPS returned an unexpected status code.</exception>
    /// <exception cref="Exceptions.UspsTransportException">The USPS service could not be reached.</exception>
    /// <exception cref="Exceptions.UspsSerializationException">The USPS response could not be parsed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<AddressValidationResult> ValidateAsync(AddressInput address, CancellationToken cancellationToken = default);
}
