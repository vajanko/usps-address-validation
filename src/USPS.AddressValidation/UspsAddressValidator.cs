using System.Globalization;
using System.Net;
using System.Text.Json;
using USPS.AddressValidation.Exceptions;
using USPS.AddressValidation.Internal;
using USPS.AddressValidation.Models;
using USPS.AddressValidation.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace USPS.AddressValidation;

/// <summary>
/// Default <see cref="IUspsAddressValidator"/>, backed by <c>GET /addresses/v3/address</c> on the
/// USPS Addresses 3.0 API.
/// </summary>
/// <remarks>
/// The instance is stateless apart from the cached OAuth token held by the token provider, and is
/// safe to use concurrently and to register as a singleton.
/// </remarks>
public sealed class UspsAddressValidator : IUspsAddressValidator
{
    internal const string AddressPath = "addresses/v3/address";

    private readonly Func<HttpClient> _httpClientAccessor;
    private readonly IUspsTokenProvider _tokenProvider;
    private readonly UspsOptions _options;
    private readonly ILogger<UspsAddressValidator> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="httpClient">Client used to call the Addresses API.</param>
    /// <param name="tokenProvider">Supplies the OAuth access token.</param>
    /// <param name="options">USPS credentials and endpoint configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="UspsConfigurationException">The options are missing a credential or otherwise unusable.</exception>
    public UspsAddressValidator(
        HttpClient httpClient,
        IUspsTokenProvider tokenProvider,
        IOptions<UspsOptions> options,
        ILogger<UspsAddressValidator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(options);

        _httpClientAccessor = () => httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value ?? throw new UspsConfigurationException("USPS options were not configured.");
        _options.Validate();
        _logger = logger ?? NullLogger<UspsAddressValidator>.Instance;
    }

    /// <summary>
    /// Initializes a new instance that resolves a fresh <see cref="HttpClient"/> for every request,
    /// so handler rotation keeps working even though the validator itself is long-lived.
    /// </summary>
    /// <param name="httpClientFactory">Factory for the named client <see cref="UspsOptions.HttpClientName"/>.</param>
    /// <param name="tokenProvider">Supplies the OAuth access token.</param>
    /// <param name="options">USPS credentials and endpoint configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="UspsConfigurationException">The options are missing a credential or otherwise unusable.</exception>
    public UspsAddressValidator(
        IHttpClientFactory httpClientFactory,
        IUspsTokenProvider tokenProvider,
        IOptions<UspsOptions> options,
        ILogger<UspsAddressValidator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(options);

        _httpClientAccessor = () => httpClientFactory.CreateClient(UspsOptions.HttpClientName);
        _tokenProvider = tokenProvider;
        _options = options.Value ?? throw new UspsConfigurationException("USPS options were not configured.");
        _options.Validate();
        _logger = logger ?? NullLogger<UspsAddressValidator>.Instance;
    }

    /// <summary>
    /// Creates a validator with its own <see cref="HttpClient"/> and token provider. Convenient for
    /// small applications and scripts; prefer the dependency-injection registration in long-running
    /// services so the underlying handler is pooled.
    /// </summary>
    /// <param name="options">USPS credentials and endpoint configuration.</param>
    /// <param name="logger">Optional logger.</param>
    public static UspsAddressValidator Create(UspsOptions options, ILoggerFactory? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        var httpClient = new HttpClient { Timeout = options.Timeout };
        var tokenProvider = new UspsTokenProvider(
            httpClient, wrapped, timeProvider: null, logger?.CreateLogger<UspsTokenProvider>());

        return new UspsAddressValidator(
            httpClient, tokenProvider, wrapped, logger?.CreateLogger<UspsAddressValidator>());
    }

    /// <inheritdoc />
    public async Task<AddressValidationResult> ValidateAsync(
        AddressInput address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        var localErrors = AddressInputValidator.Validate(address);
        if (localErrors.Count > 0)
        {
            _logger.LogDebug(
                "Address rejected locally before calling USPS: {Errors}",
                string.Join("; ", localErrors.Select(e => e.ToString())));
            return AddressValidationResult.FromLocalValidation(address, localErrors);
        }

        var requestUri = BuildRequestUri(address);

        using var response = await SendWithTokenAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => ParseSuccess(address, body),

            // USPS reports an unverifiable address with 400/404 and an error envelope. That is an
            // answer about the address, not a failure of the call, so it comes back as a result.
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound => ParseAddressError(address, body),

            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                throw BuildAuthenticationException(response.StatusCode, body),

            HttpStatusCode.TooManyRequests => throw new UspsRateLimitException(
                "USPS rejected the request because too many requests were sent in a short period.",
                response.Headers.RetryAfter?.Delta,
                ExtractErrorCode(body),
                Truncate(body)),

            _ => throw new UspsApiException(
                $"The USPS Addresses API returned an unexpected status code " +
                $"{(int)response.StatusCode} {response.StatusCode}. {ExtractErrorMessage(body)}".TrimEnd(),
                response.StatusCode,
                ExtractErrorCode(body),
                Truncate(body)),
        };
    }

    internal Uri BuildRequestUri(AddressInput address)
    {
        // USPS takes the two halves of a ZIP+4 as separate parameters; callers supply one value.
        var (zip5, zipPlus4) = ZipCodeParser.Split(address.ZipCode);

        var query = new List<string>(8);
        Add(query, "firm", address.Firm);
        Add(query, "streetAddress", address.StreetAddress);
        Add(query, "secondaryAddress", address.SecondaryAddress);
        Add(query, "city", address.City);
        Add(query, "state", address.State?.ToUpperInvariant());
        Add(query, "urbanization", address.Urbanization);
        Add(query, "ZIPCode", zip5);
        Add(query, "ZIPPlus4", zipPlus4);

        var relative = $"{AddressPath}?{string.Join("&", query)}";
        return new Uri(_options.ResolvedBaseAddress, relative);

        static void Add(List<string> query, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
            }
        }
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        var response = await SendAsync(requestUri, cancellationToken).ConfigureAwait(false);

        // A token can be revoked or expire early. Refresh once and retry before giving up.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogDebug("USPS returned 401; refreshing the access token and retrying once.");
            response.Dispose();
            _tokenProvider.Invalidate();
            response = await SendAsync(requestUri, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new("application/json"));
        request.Headers.Authorization = new("Bearer", token);
        if (!string.IsNullOrWhiteSpace(_options.UserId))
        {
            request.Headers.TryAddWithoutValidation("X-User-Id", _options.UserId);
        }

        try
        {
            return await _httpClientAccessor().SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new UspsTransportException(
                $"Could not reach the USPS Addresses API at '{requestUri.GetLeftPart(UriPartial.Path)}'.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UspsTransportException(
                $"The request to the USPS Addresses API at '{requestUri.GetLeftPart(UriPartial.Path)}' timed out " +
                $"after {_options.Timeout.TotalSeconds:0.#}s.", ex);
        }
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new UspsTransportException("The USPS response body could not be read.", ex);
        }
        catch (IOException ex)
        {
            throw new UspsTransportException("The USPS response body could not be read.", ex);
        }
    }

    private static AddressValidationResult ParseSuccess(AddressInput input, string body)
    {
        AddressResponseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AddressResponseDto>(body, UspsJson.Options);
        }
        catch (JsonException ex)
        {
            throw new UspsSerializationException(
                "The USPS Addresses API returned a success response that could not be parsed.", Truncate(body), ex);
        }

        if (dto is null)
        {
            throw new UspsSerializationException(
                "The USPS Addresses API returned an empty success response.", Truncate(body));
        }

        // USPS returns the firm alongside the address rather than inside it; fold it in so callers
        // have one complete address object.
        var address = dto.Address;
        if (address is not null && string.IsNullOrWhiteSpace(address.Firm) && !string.IsNullOrWhiteSpace(dto.Firm))
        {
            address = address with { Firm = dto.Firm };
        }

        var status = UspsStatusMapper.FromSuccessResponse(dto);
        var errors = status is AddressValidationStatus.Validated or AddressValidationStatus.DefaultAddress
            ? []
            : BuildErrorsFromCodes(status, dto);

        return AddressValidationResult.FromUsps(
            status,
            input,
            address,
            dto.AdditionalInfo,
            dto.Corrections ?? [],
            dto.Matches ?? [],
            dto.Warnings ?? [],
            errors,
            AddressChangeDetector.Detect(input, address));
    }

    private static IReadOnlyList<AddressValidationError> BuildErrorsFromCodes(
        AddressValidationStatus status,
        AddressResponseDto dto)
    {
        var message = dto.AdditionalInfo?.ReturnCodeText
            ?? dto.Corrections?.FirstOrDefault()?.Text
            ?? $"USPS could not validate the address ({status}).";

        var code = dto.AdditionalInfo?.ReturnCode?.ToString(CultureInfo.InvariantCulture)
            ?? dto.Corrections?.FirstOrDefault()?.Code;

        return [new AddressValidationError(message, ValidationErrorSource.Usps, code)];
    }

    private static AddressValidationResult ParseAddressError(AddressInput input, string body)
    {
        ErrorResponseDto? dto = null;
        try
        {
            dto = JsonSerializer.Deserialize<ErrorResponseDto>(body, UspsJson.Options);
        }
        catch (JsonException)
        {
            // USPS occasionally answers with a non-JSON body; fall back to the raw text below.
        }

        var message = dto?.Error?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = string.IsNullOrWhiteSpace(body)
                ? "USPS could not validate the address."
                : Truncate(body, 500);
        }

        var status = UspsStatusMapper.FromErrorMessage(message);
        var errors = new List<AddressValidationError>
        {
            new(message, ValidationErrorSource.Usps, dto?.Error?.Code),
        };

        foreach (var detail in dto?.Error?.Errors ?? [])
        {
            var detailMessage = detail.Detail ?? detail.Title;
            if (!string.IsNullOrWhiteSpace(detailMessage))
            {
                errors.Add(new AddressValidationError(
                    detailMessage, ValidationErrorSource.Usps, detail.Code, detail.Source?.Parameter));
            }
        }

        return AddressValidationResult.FromUsps(
            status, input, address: null, additionalInfo: null,
            corrections: [], matches: [], warnings: [], errors, changes: []);
    }

    private static UspsAuthenticationException BuildAuthenticationException(HttpStatusCode statusCode, string body)
    {
        var detail = ExtractErrorMessage(body);
        var hint = statusCode == HttpStatusCode.Forbidden
            ? "The credentials were accepted but the application is not authorized for the Addresses API. " +
              "Check that the app holds an Addresses API license and that the 'addresses' scope is granted."
            : "Check the configured Consumer Key and Consumer Secret.";

        return new UspsAuthenticationException(
            $"USPS rejected the Addresses API request ({(int)statusCode} {statusCode}). {detail} {hint}".Replace("  ", " "),
            statusCode,
            ExtractErrorCode(body),
            Truncate(body));
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ErrorResponseDto>(body, UspsJson.Options)?.Error?.Message ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string? ExtractErrorCode(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ErrorResponseDto>(body, UspsJson.Options)?.Error?.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int max = 2000) =>
        value.Length <= max ? value : value[..max] + "...";
}
