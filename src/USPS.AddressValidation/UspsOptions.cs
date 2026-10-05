using USPS.AddressValidation.Exceptions;

namespace USPS.AddressValidation;

/// <summary>
/// The USPS environment to talk to.
/// </summary>
public enum UspsEnvironment
{
    /// <summary>The production endpoint, <c>https://apis.usps.com</c>.</summary>
    Production = 0,

    /// <summary>The testing (TEM) endpoint, <c>https://apis-tem.usps.com</c>.</summary>
    Testing,
}

/// <summary>
/// Configuration for <see cref="UspsAddressValidator"/>.
/// </summary>
/// <remarks>
/// <see cref="ClientId"/> and <see cref="ClientSecret"/> are the Consumer Key and Consumer Secret
/// of an application registered on the USPS Developer Portal that holds an Addresses API license.
/// </remarks>
public sealed class UspsOptions
{
    /// <summary>The configuration section these options are conventionally bound from.</summary>
    public const string SectionName = "Usps";

    /// <summary>
    /// Name under which the USPS services resolve their <see cref="HttpClient"/> from an
    /// <see cref="IHttpClientFactory"/>.
    /// </summary>
    public const string HttpClientName = "USPS.AddressValidation";

    /// <summary>Base address of the USPS production API.</summary>
    public static readonly Uri ProductionBaseAddress = new("https://apis.usps.com/");

    /// <summary>Base address of the USPS testing (TEM) API.</summary>
    public static readonly Uri TestingBaseAddress = new("https://apis-tem.usps.com/");

    /// <summary>The application's Consumer Key.</summary>
    public string? ClientId { get; set; }

    /// <summary>The application's Consumer Secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Which USPS environment to use. Ignored when <see cref="BaseAddress"/> is set explicitly.
    /// </summary>
    public UspsEnvironment Environment { get; set; } = UspsEnvironment.Production;

    /// <summary>
    /// Overrides the endpoint derived from <see cref="Environment"/>. Useful for tests and proxies.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// OAuth scope requested with the token. Defaults to <c>addresses</c>; leave empty to let USPS
    /// apply the application's default scope.
    /// </summary>
    public string? Scope { get; set; } = "addresses";

    /// <summary>Per-request timeout. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long before a token's stated expiry it is treated as expired, so a request is never sent
    /// with a token about to lapse. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan TokenExpiryBuffer { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Optional value for the <c>X-User-Id</c> header, when the USPS account requires it.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>The resolved endpoint: <see cref="BaseAddress"/> when set, otherwise the environment default.</summary>
    public Uri ResolvedBaseAddress => BaseAddress ?? Environment switch
    {
        UspsEnvironment.Testing => TestingBaseAddress,
        _ => ProductionBaseAddress,
    };

    /// <summary>
    /// Throws when the options cannot produce a working client.
    /// </summary>
    /// <exception cref="UspsConfigurationException">A required value is missing or invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new UspsConfigurationException(
                $"USPS {nameof(ClientId)} (Consumer Key) is not configured. Set {SectionName}:{nameof(ClientId)}.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new UspsConfigurationException(
                $"USPS {nameof(ClientSecret)} (Consumer Secret) is not configured. Set {SectionName}:{nameof(ClientSecret)}.");
        }

        if (BaseAddress is not null && !BaseAddress.IsAbsoluteUri)
        {
            throw new UspsConfigurationException(
                $"USPS {nameof(BaseAddress)} must be an absolute URI but was '{BaseAddress}'.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new UspsConfigurationException($"USPS {nameof(Timeout)} must be greater than zero.");
        }

        if (TokenExpiryBuffer < TimeSpan.Zero)
        {
            throw new UspsConfigurationException($"USPS {nameof(TokenExpiryBuffer)} must not be negative.");
        }
    }
}
