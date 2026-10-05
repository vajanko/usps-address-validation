namespace USPS.AddressValidation;

/// <summary>
/// Supplies OAuth 2.0 access tokens for the USPS APIs.
/// </summary>
/// <remarks>
/// The default implementation, <see cref="UspsTokenProvider"/>, caches a token until shortly before
/// it expires. Substitute your own to share a token across processes or to plug in a different
/// grant type.
/// </remarks>
public interface IUspsTokenProvider
{
    /// <summary>Returns a valid access token, obtaining or renewing one if required.</summary>
    /// <exception cref="Exceptions.UspsConfigurationException">Credentials are missing.</exception>
    /// <exception cref="Exceptions.UspsAuthenticationException">USPS rejected the credentials.</exception>
    /// <exception cref="Exceptions.UspsTransportException">The token endpoint could not be reached.</exception>
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the cached token so the next call obtains a fresh one. Called automatically when
    /// USPS answers a resource request with <c>401</c>.
    /// </summary>
    void Invalidate();
}
