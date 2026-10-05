using System.Text.Json.Serialization;

namespace USPS.AddressValidation.Internal;

/// <summary>Client-credentials grant body for <c>POST /oauth2/v3/token</c>.</summary>
internal sealed class TokenRequestDto
{
    [JsonPropertyName("grant_type")]
    public string GrantType { get; init; } = "client_credentials";

    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("client_secret")]
    public required string ClientSecret { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

/// <summary>Successful token response.</summary>
internal sealed class TokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    /// <summary>Lifetime of the access token in seconds.</summary>
    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
