using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using USPS.AddressValidation.Exceptions;
using USPS.AddressValidation.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace USPS.AddressValidation;

/// <summary>
/// Obtains USPS access tokens with the OAuth 2.0 client-credentials grant and caches them until
/// shortly before expiry. Safe for concurrent use; only one token request is in flight at a time.
/// </summary>
public sealed class UspsTokenProvider : IUspsTokenProvider, IDisposable
{
    internal const string TokenPath = "oauth2/v3/token";

    private readonly Func<HttpClient> _httpClientAccessor;
    private readonly UspsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UspsTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="httpClient">Client used to call the USPS token endpoint.</param>
    /// <param name="options">USPS credentials and endpoint configuration.</param>
    /// <param name="timeProvider">Clock used for token expiry. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="UspsConfigurationException">The options are missing a credential or otherwise unusable.</exception>
    public UspsTokenProvider(
        HttpClient httpClient,
        IOptions<UspsOptions> options,
        TimeProvider? timeProvider = null,
        ILogger<UspsTokenProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClientAccessor = () => httpClient;
        _options = options.Value ?? throw new UspsConfigurationException("USPS options were not configured.");
        _options.Validate();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<UspsTokenProvider>.Instance;
    }

    /// <summary>
    /// Initializes a new instance that resolves a fresh <see cref="HttpClient"/> for every token
    /// request, so handler rotation keeps working even though the provider itself is long-lived.
    /// </summary>
    /// <param name="httpClientFactory">Factory for the named client <see cref="UspsOptions.HttpClientName"/>.</param>
    /// <param name="options">USPS credentials and endpoint configuration.</param>
    /// <param name="timeProvider">Clock used for token expiry. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="UspsConfigurationException">The options are missing a credential or otherwise unusable.</exception>
    public UspsTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<UspsOptions> options,
        TimeProvider? timeProvider = null,
        ILogger<UspsTokenProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);

        _httpClientAccessor = () => httpClientFactory.CreateClient(UspsOptions.HttpClientName);
        _options = options.Value ?? throw new UspsConfigurationException("USPS options were not configured.");
        _options.Validate();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<UspsTokenProvider>.Instance;
    }

    /// <inheritdoc />
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have refreshed the token while we waited on the gate.
            if (TryGetCachedToken(out cached))
            {
                return cached;
            }

            var token = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            _accessToken = token.AccessToken;
            _expiresAt = token.ExpiresAt;
            return token.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        _accessToken = null;
        _expiresAt = default;
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private bool TryGetCachedToken(out string token)
    {
        var cached = _accessToken;
        if (cached is not null && _timeProvider.GetUtcNow() < _expiresAt)
        {
            token = cached;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<(string AccessToken, DateTimeOffset ExpiresAt)> RequestTokenAsync(
        CancellationToken cancellationToken)
    {
        var tokenUri = new Uri(_options.ResolvedBaseAddress, TokenPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = JsonContent.Create(
                new TokenRequestDto
                {
                    ClientId = _options.ClientId!,
                    ClientSecret = _options.ClientSecret!,
                    Scope = string.IsNullOrWhiteSpace(_options.Scope) ? null : _options.Scope,
                },
                options: UspsJson.Options),
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClientAccessor().SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new UspsTransportException($"Could not reach the USPS token endpoint at '{tokenUri}'.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UspsTransportException($"The request to the USPS token endpoint at '{tokenUri}' timed out.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw BuildAuthenticationException(response.StatusCode, body);
            }

            TokenResponseDto? token;
            try
            {
                token = JsonSerializer.Deserialize<TokenResponseDto>(body, UspsJson.Options);
            }
            catch (JsonException ex)
            {
                throw new UspsSerializationException(
                    "The USPS token endpoint returned a body that could not be parsed.", Truncate(body), ex);
            }

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new UspsAuthenticationException(
                    "The USPS token endpoint returned no access token.",
                    response.StatusCode,
                    responseBody: Truncate(body));
            }

            var lifetime = token.ExpiresIn is > 0
                ? TimeSpan.FromSeconds(token.ExpiresIn.Value)
                : TimeSpan.FromMinutes(5);

            // Never hand out a token that is about to lapse mid-request.
            var effective = lifetime - _options.TokenExpiryBuffer;
            if (effective <= TimeSpan.Zero)
            {
                effective = TimeSpan.FromTicks(lifetime.Ticks / 2);
            }

            _logger.LogDebug("Obtained a USPS access token valid for {Seconds}s.", lifetime.TotalSeconds);
            return (token.AccessToken, _timeProvider.GetUtcNow().Add(effective));
        }
    }

    private static UspsAuthenticationException BuildAuthenticationException(HttpStatusCode statusCode, string body)
    {
        string? code = null;
        var description = string.Empty;

        try
        {
            var oauthError = JsonSerializer.Deserialize<OAuthErrorDto>(body, UspsJson.Options);
            if (!string.IsNullOrWhiteSpace(oauthError?.Error))
            {
                code = oauthError.Error;
                description = oauthError.ErrorDescription ?? string.Empty;
            }
            else
            {
                var apiError = JsonSerializer.Deserialize<ErrorResponseDto>(body, UspsJson.Options);
                code = apiError?.Error?.Code;
                description = apiError?.Error?.Message ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Not a recognizable error envelope; fall back to a status-code-only message.
        }

        var suffix = string.IsNullOrWhiteSpace(description) ? string.Empty : $" {description}";
        return new UspsAuthenticationException(
            $"USPS refused to issue an access token ({(int)statusCode} {statusCode}).{suffix} " +
            "Check the configured Consumer Key and Consumer Secret, and that the application has an " +
            "Addresses API license.",
            statusCode,
            code,
            Truncate(body));
    }

    private static string Truncate(string value, int max = 2000) =>
        value.Length <= max ? value : value[..max] + "...";
}
