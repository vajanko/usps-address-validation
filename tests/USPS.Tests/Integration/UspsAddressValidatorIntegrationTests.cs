using USPS.AddressValidation;
using USPS.AddressValidation.Exceptions;
using USPS.AddressValidation.Models;

namespace USPS.Tests.Integration;

/// <summary>
/// Live tests against the real USPS Addresses API.
/// </summary>
/// <remarks>
/// These run only when USPS credentials are configured; see <see cref="UspsCredentials"/>. Without
/// them each test reports as skipped, so <c>dotnet test</c> stays green on a machine with no USPS
/// access. Run just this set with
/// <c>dotnet test --filter-trait "Category=Integration"</c>.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class UspsAddressValidatorIntegrationTests
{
    private static UspsAddressValidator CreateValidator() =>
        UspsAddressValidator.Create(UspsCredentials.RequireOptions());

    [Fact]
    public async Task Known_good_address_is_validated_and_completed()
    {
        var validator = CreateValidator();

        // A well-known USPS example address, deliberately submitted without a ZIP Code.
        var result = await validator.ValidateAsync(
            AddressInput.Create("3120 M St NW", city: "Washington", state: "DC"),
            TestContext.Current.CancellationToken);

        Assert.Equal(AddressValidationStatus.Validated, result.Status);
        Assert.True(result.IsValid, result.ErrorMessage);

        var address = result.Address!;
        Assert.Equal("DC", address.State);
        Assert.Equal("20007", address.ZipCode);
        Assert.False(string.IsNullOrWhiteSpace(address.ZipPlus4));

        // USPS filled in the ZIP Code that was missing from the input.
        Assert.True(result.WasModified);
        Assert.Contains(result.Changes, c => c.Field == nameof(UspsAddress.ZipCode));
        Assert.Contains(result.Changes, c => c.Field == nameof(UspsAddress.ZipPlus4));

        Assert.True(result.AdditionalInfo?.IsDeliveryPointConfirmed);
    }

    [Fact]
    public async Task Lowercase_and_unabbreviated_input_is_standardized()
    {
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(
            AddressInput.Create("475 lenfant plaza sw", city: "washington", state: "dc"),
            TestContext.Current.CancellationToken);

        Assert.True(result.HasAddress, result.ErrorMessage);
        Assert.Equal("WASHINGTON", result.Address!.City);
        Assert.Equal("DC", result.Address.State);
    }

    [Fact]
    public async Task Address_that_does_not_exist_is_reported_as_a_failed_result()
    {
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(
            AddressInput.Create("99999 Nonexistent Parkway", city: "Washington", state: "DC"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.NotEqual(AddressValidationStatus.Validated, result.Status);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.Equal(ValidationErrorSource.Usps, e.Source));
    }

    [Fact]
    public async Task Address_in_a_zip_code_that_does_not_serve_it_is_rejected()
    {
        var validator = CreateValidator();

        // A real street in Washington DC paired with a New York ZIP Code.
        var result = await validator.ValidateAsync(
            AddressInput.Create("3120 M St NW", zipCode: "10018"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Locally_invalid_input_never_reaches_the_service()
    {
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(
            AddressInput.Create("3120 M St NW", city: "Washington", state: "ZZ"),
            TestContext.Current.CancellationToken);

        Assert.Equal(AddressValidationStatus.InvalidInput, result.Status);
        Assert.All(result.Errors, e => Assert.Equal(ValidationErrorSource.Local, e.Source));
    }

    [Fact]
    public async Task Wrong_credentials_throw_an_authentication_exception()
    {
        var options = UspsCredentials.RequireOptions();
        var validator = UspsAddressValidator.Create(new UspsOptions
        {
            ClientId = "definitely-not-a-real-consumer-key",
            ClientSecret = "definitely-not-a-real-consumer-secret",
            Environment = options.Environment,
        });

        await Assert.ThrowsAsync<UspsAuthenticationException>(
            () => validator.ValidateAsync(
                AddressInput.Create("3120 M St NW", city: "Washington", state: "DC"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unreachable_host_throws_a_transport_exception()
    {
        var options = UspsCredentials.RequireOptions();
        var validator = UspsAddressValidator.Create(new UspsOptions
        {
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            BaseAddress = new Uri("https://usps.invalid/"),
            Timeout = TimeSpan.FromSeconds(10),
        });

        await Assert.ThrowsAsync<UspsTransportException>(
            () => validator.ValidateAsync(
                AddressInput.Create("3120 M St NW", city: "Washington", state: "DC"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Repeated_calls_reuse_a_single_access_token()
    {
        var validator = CreateValidator();
        var input = AddressInput.Create("3120 M St NW", city: "Washington", state: "DC");

        var first = await validator.ValidateAsync(input, TestContext.Current.CancellationToken);
        var second = await validator.ValidateAsync(input, TestContext.Current.CancellationToken);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Address?.FullZipCode, second.Address?.FullZipCode);
    }
}
