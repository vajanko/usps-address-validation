using System.Net;
using System.Text.Json;
using USPS.AddressValidation.Exceptions;
using USPS.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace USPS.Tests.Unit;

/// <summary>
/// Token acquisition, caching and renewal.
/// </summary>
public sealed class UspsTokenProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Posts_the_client_credentials_grant_to_the_documented_endpoint()
    {
        using var harness = TestHarness.Raw(h => h.EnqueueToken());

        var token = await harness.GetAccessTokenAsync();

        Assert.Equal("test-access-token", token);

        var request = Assert.Single(harness.Handler.TokenRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/oauth2/v3/token", request.RequestUri!.AbsolutePath);

        var body = harness.Handler.BodyOf(request);
        Assert.NotNull(body);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("client_credentials", json.RootElement.GetProperty("grant_type").GetString());
        Assert.Equal("test-consumer-key", json.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("test-consumer-secret", json.RootElement.GetProperty("client_secret").GetString());
        Assert.Equal("addresses", json.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Caches_the_token_across_calls()
    {
        var time = new FakeTimeProvider(Now);
        using var harness = TestHarness.Raw(h => h.EnqueueToken(expiresIn: 3600), timeProvider: time);

        var first = await harness.GetAccessTokenAsync();
        time.Advance(TimeSpan.FromMinutes(30));
        var second = await harness.GetAccessTokenAsync();

        Assert.Equal(first, second);
        Assert.Single(harness.Handler.TokenRequests);
    }

    [Fact]
    public async Task Renews_the_token_once_it_has_expired()
    {
        var time = new FakeTimeProvider(Now);
        using var harness = TestHarness.Raw(
            h => h.EnqueueToken("first", expiresIn: 3600).EnqueueToken("second", expiresIn: 3600),
            timeProvider: time);

        Assert.Equal("first", await harness.GetAccessTokenAsync());
        time.Advance(TimeSpan.FromHours(1));
        Assert.Equal("second", await harness.GetAccessTokenAsync());

        Assert.Equal(2, harness.Handler.TokenRequests.Count);
    }

    [Fact]
    public async Task Renews_early_by_the_configured_expiry_buffer()
    {
        var time = new FakeTimeProvider(Now);
        var options = TestHarness.DefaultOptions();
        options.TokenExpiryBuffer = TimeSpan.FromMinutes(5);

        using var harness = TestHarness.Raw(
            h => h.EnqueueToken("first", expiresIn: 600).EnqueueToken("second", expiresIn: 600),
            options,
            time);

        Assert.Equal("first", await harness.GetAccessTokenAsync());

        // Four minutes in, the token is still considered usable.
        time.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal("first", await harness.GetAccessTokenAsync());

        // Six minutes in, it is inside the five-minute buffer and gets renewed early.
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("second", await harness.GetAccessTokenAsync());
    }

    [Fact]
    public async Task Invalidate_forces_a_fresh_token()
    {
        var time = new FakeTimeProvider(Now);
        using var harness = TestHarness.Raw(
            h => h.EnqueueToken("first").EnqueueToken("second"),
            timeProvider: time);

        Assert.Equal("first", await harness.GetAccessTokenAsync());
        harness.TokenProvider.Invalidate();
        Assert.Equal("second", await harness.GetAccessTokenAsync());
    }

    [Fact]
    public async Task Concurrent_callers_share_a_single_token_request()
    {
        var time = new FakeTimeProvider(Now);
        using var harness = TestHarness.Raw(h => h.EnqueueToken(expiresIn: 3600), timeProvider: time);

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => harness.GetAccessTokenAsync().AsTask()));

        Assert.All(tokens, t => Assert.Equal("test-access-token", t));
        Assert.Single(harness.Handler.TokenRequests);
    }

    [Fact]
    public async Task Rejected_credentials_throw_an_authentication_exception()
    {
        using var harness = TestHarness.Raw(
            h => h.EnqueueJson(HttpStatusCode.Unauthorized, TestPayloads.OAuthInvalidClientError));

        var exception = await Assert.ThrowsAsync<UspsAuthenticationException>(
            async () => await harness.GetAccessTokenAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("invalid_client", exception.ErrorCode);
        Assert.Contains("Consumer Key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Token_response_without_an_access_token_throws()
    {
        using var harness = TestHarness.Raw(h => h.EnqueueJson(HttpStatusCode.OK, """{"token_type":"Bearer"}"""));

        await Assert.ThrowsAsync<UspsAuthenticationException>(
            async () => await harness.GetAccessTokenAsync());
    }

    [Fact]
    public async Task Unreachable_token_endpoint_throws_a_transport_exception()
    {
        using var harness = TestHarness.Raw(h => h.EnqueueThrow(new HttpRequestException("No route to host.")));

        var exception = await Assert.ThrowsAsync<UspsTransportException>(
            async () => await harness.GetAccessTokenAsync());

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task Expires_in_returned_as_a_string_is_still_honoured()
    {
        var time = new FakeTimeProvider(Now);
        using var harness = TestHarness.Raw(
            h => h.EnqueueJson(
                HttpStatusCode.OK,
                """{"access_token":"tok","token_type":"Bearer","expires_in":"3600"}"""),
            timeProvider: time);

        Assert.Equal("tok", await harness.GetAccessTokenAsync());

        time.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal("tok", await harness.GetAccessTokenAsync());
        Assert.Single(harness.Handler.TokenRequests);
    }
}
