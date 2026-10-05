using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace USPS.AddressValidation.DependencyInjection;

/// <summary>
/// Registers the USPS address validation client with a dependency-injection container.
/// </summary>
public static class UspsServiceCollectionExtensions
{
    /// <summary>Name of the <see cref="HttpClient"/> the USPS services resolve.</summary>
    public const string HttpClientName = UspsOptions.HttpClientName;

    /// <summary>
    /// Registers <see cref="IUspsAddressValidator"/> and <see cref="IUspsTokenProvider"/> using
    /// options configured in code.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Callback that populates <see cref="UspsOptions"/>.</param>
    public static IServiceCollection AddUspsAddressValidation(
        this IServiceCollection services,
        Action<UspsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        return AddCore(services);
    }

    /// <summary>
    /// Registers <see cref="IUspsAddressValidator"/> and <see cref="IUspsTokenProvider"/> using
    /// options bound from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The section holding the USPS settings, conventionally <c>Usps</c>
    /// (see <see cref="UspsOptions.SectionName"/>).
    /// </param>
    public static IServiceCollection AddUspsAddressValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<UspsOptions>(configuration);
        return AddCore(services);
    }

    private static IServiceCollection AddCore(IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<UspsOptions>>().Value;
            client.BaseAddress = options.ResolvedBaseAddress;
            client.Timeout = options.Timeout;
        });

        // Both services are singletons so the access token is cached across calls; each resolves a
        // pooled HttpClient per request, so the factory keeps rotating handlers underneath.
        services.TryAddSingleton<IUspsTokenProvider>(provider => new UspsTokenProvider(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IOptions<UspsOptions>>(),
            provider.GetService<TimeProvider>(),
            provider.GetService<ILogger<UspsTokenProvider>>()));

        services.TryAddSingleton<IUspsAddressValidator>(provider => new UspsAddressValidator(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IUspsTokenProvider>(),
            provider.GetRequiredService<IOptions<UspsOptions>>(),
            provider.GetService<ILogger<UspsAddressValidator>>()));

        return services;
    }
}
