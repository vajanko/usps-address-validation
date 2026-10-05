using USPS.AddressValidation;
using USPS.AddressValidation.Models;
using Microsoft.Extensions.Options;

namespace USPS.Tests.Infrastructure;

/// <summary>
/// Wires a <see cref="UspsAddressValidator"/> up to a <see cref="StubHttpMessageHandler"/> so a test
/// can script the USPS conversation and then inspect what was sent.
/// </summary>
internal sealed class TestHarness : IDisposable
{
    private readonly HttpClient _httpClient;

    private TestHarness(StubHttpMessageHandler handler, UspsOptions options, TimeProvider timeProvider)
    {
        Handler = handler;
        Options = options;
        _httpClient = new HttpClient(handler) { Timeout = options.Timeout };

        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        TokenProvider = new UspsTokenProvider(_httpClient, wrapped, timeProvider);
        Validator = new UspsAddressValidator(_httpClient, TokenProvider, wrapped);
    }

    public StubHttpMessageHandler Handler { get; }

    public UspsOptions Options { get; }

    public UspsTokenProvider TokenProvider { get; }

    public UspsAddressValidator Validator { get; }

    /// <summary>
    /// Validates through <see cref="Validator"/>, passing the running test's cancellation token so
    /// a hung test is torn down promptly.
    /// </summary>
    public Task<AddressValidationResult> ValidateAsync(AddressInput input) =>
        Validator.ValidateAsync(input, TestContext.Current.CancellationToken);

    /// <summary>Fetches a token through <see cref="TokenProvider"/> with the running test's cancellation token.</summary>
    public ValueTask<string> GetAccessTokenAsync() =>
        TokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

    /// <summary>Builds a harness whose token endpoint has already been primed with a valid token.</summary>
    public static TestHarness WithToken(
        Action<StubHttpMessageHandler> script,
        UspsOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueToken();
        script(handler);
        return new TestHarness(handler, options ?? DefaultOptions(), timeProvider ?? TimeProvider.System);
    }

    /// <summary>Builds a harness with nothing scripted; the caller drives the handler.</summary>
    public static TestHarness Raw(
        Action<StubHttpMessageHandler> script,
        UspsOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        var handler = new StubHttpMessageHandler();
        script(handler);
        return new TestHarness(handler, options ?? DefaultOptions(), timeProvider ?? TimeProvider.System);
    }

    public static UspsOptions DefaultOptions() => new()
    {
        ClientId = "test-consumer-key",
        ClientSecret = "test-consumer-secret",
        BaseAddress = new Uri("https://usps.test/"),
        Timeout = TimeSpan.FromSeconds(5),
    };

    public void Dispose()
    {
        TokenProvider.Dispose();
        _httpClient.Dispose();
    }
}
