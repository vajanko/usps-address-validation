using USPS.AddressValidation;
using USPS.AddressValidation.DependencyInjection;
using USPS.AddressValidation.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace USPS.Tests.Unit;

/// <summary>
/// Configuration handling and container registration.
/// </summary>
public sealed class UspsOptionsTests
{
    [Fact]
    public void Defaults_to_the_production_endpoint()
    {
        var options = new UspsOptions { ClientId = "id", ClientSecret = "secret" };

        Assert.Equal(new Uri("https://apis.usps.com/"), options.ResolvedBaseAddress);
    }

    [Fact]
    public void Testing_environment_resolves_to_the_tem_endpoint()
    {
        var options = new UspsOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            Environment = UspsEnvironment.Testing,
        };

        Assert.Equal(new Uri("https://apis-tem.usps.com/"), options.ResolvedBaseAddress);
    }

    [Fact]
    public void Explicit_base_address_wins_over_the_environment()
    {
        var options = new UspsOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            Environment = UspsEnvironment.Testing,
            BaseAddress = new Uri("https://proxy.internal/"),
        };

        Assert.Equal(new Uri("https://proxy.internal/"), options.ResolvedBaseAddress);
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("", "secret")]
    [InlineData("  ", "secret")]
    [InlineData("id", null)]
    [InlineData("id", "")]
    public void Validate_rejects_missing_credentials(string? clientId, string? clientSecret)
    {
        var options = new UspsOptions { ClientId = clientId, ClientSecret = clientSecret };

        Assert.Throws<UspsConfigurationException>(options.Validate);
    }

    [Fact]
    public void Validate_rejects_a_relative_base_address()
    {
        var options = new UspsOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            BaseAddress = new Uri("/addresses", UriKind.Relative),
        };

        Assert.Throws<UspsConfigurationException>(options.Validate);
    }

    [Fact]
    public void Validate_rejects_a_non_positive_timeout()
    {
        var options = new UspsOptions { ClientId = "id", ClientSecret = "secret", Timeout = TimeSpan.Zero };

        Assert.Throws<UspsConfigurationException>(options.Validate);
    }

    [Fact]
    public void Validate_accepts_a_complete_configuration()
    {
        var options = new UspsOptions { ClientId = "id", ClientSecret = "secret" };

        options.Validate();
    }

    [Fact]
    public void Container_resolves_the_validator_from_a_configuration_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Usps:ClientId"] = "consumer-key",
                ["Usps:ClientSecret"] = "consumer-secret",
                ["Usps:Environment"] = nameof(UspsEnvironment.Testing),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddUspsAddressValidation(configuration.GetSection(UspsOptions.SectionName));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<UspsAddressValidator>(provider.GetRequiredService<IUspsAddressValidator>());
        Assert.IsType<UspsTokenProvider>(provider.GetRequiredService<IUspsTokenProvider>());

        // Singletons, so the cached access token is shared across calls.
        Assert.Same(
            provider.GetRequiredService<IUspsAddressValidator>(),
            provider.GetRequiredService<IUspsAddressValidator>());
    }

    [Fact]
    public void Container_resolves_the_validator_from_an_inline_configuration_callback()
    {
        var services = new ServiceCollection();
        services.AddUspsAddressValidation(options =>
        {
            options.ClientId = "consumer-key";
            options.ClientSecret = "consumer-secret";
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IUspsAddressValidator>());
    }

    [Fact]
    public void Container_registration_surfaces_missing_credentials_on_resolution()
    {
        var services = new ServiceCollection();
        services.AddUspsAddressValidation(options => options.ClientId = "consumer-key");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<UspsConfigurationException>(() => provider.GetRequiredService<IUspsAddressValidator>());
    }
}
