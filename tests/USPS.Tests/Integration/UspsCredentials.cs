using USPS.AddressValidation;
using Microsoft.Extensions.Configuration;

namespace USPS.Tests.Integration;

/// <summary>
/// Reads live USPS credentials for the integration tests.
/// </summary>
/// <remarks>
/// Supply them either as environment variables — <c>USPS_CLIENT_ID</c>, <c>USPS_CLIENT_SECRET</c>
/// and optionally <c>USPS_ENVIRONMENT</c> (<c>Production</c> or <c>Testing</c>) — or as user
/// secrets under the keys <c>Usps:ClientId</c> and <c>Usps:ClientSecret</c>:
/// <code>
/// dotnet user-secrets set "Usps:ClientId" "your-consumer-key" --project tests/USPS.Tests
/// dotnet user-secrets set "Usps:ClientSecret" "your-consumer-secret" --project tests/USPS.Tests
/// </code>
/// When no credentials are found every integration test skips rather than fails, so the suite still
/// runs on a machine without USPS access.
/// </remarks>
internal static class UspsCredentials
{
    private static readonly Lazy<UspsOptions?> Lazy = new(Load, isThreadSafe: true);

    /// <summary>The configured options, or <see langword="null"/> when no credentials were supplied.</summary>
    public static UspsOptions? Options => Lazy.Value;

    /// <summary>Whether live credentials are available.</summary>
    public static bool Available => Lazy.Value is not null;

    /// <summary>The reason shown when an integration test is skipped.</summary>
    public const string SkipReason =
        "No USPS credentials configured. Set USPS_CLIENT_ID and USPS_CLIENT_SECRET " +
        "(or the Usps:ClientId / Usps:ClientSecret user secrets) to run the live API tests.";

    /// <summary>Returns the options, or skips the calling test when none are configured.</summary>
    public static UspsOptions RequireOptions()
    {
        Assert.SkipUnless(Available, SkipReason);
        return Options!;
    }

    private static UspsOptions? Load()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<UspsAddressValidatorIntegrationTests>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var clientId = configuration["USPS_CLIENT_ID"] ?? configuration["Usps:ClientId"];
        var clientSecret = configuration["USPS_CLIENT_SECRET"] ?? configuration["Usps:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var environmentName = configuration["USPS_ENVIRONMENT"] ?? configuration["Usps:Environment"];
        var environment = Enum.TryParse<UspsEnvironment>(environmentName, ignoreCase: true, out var parsed)
            ? parsed
            : UspsEnvironment.Production;

        return new UspsOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Environment = environment,
            Timeout = TimeSpan.FromSeconds(30),
        };
    }
}
