using System.Text.Json.Serialization;

namespace USPS.AddressValidation.Internal;

/// <summary>Wire shape of the USPS standard error envelope.</summary>
internal sealed class ErrorResponseDto
{
    [JsonPropertyName("apiVersion")]
    public string? ApiVersion { get; init; }

    [JsonPropertyName("error")]
    public ErrorDto? Error { get; init; }
}

internal sealed class ErrorDto
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("errors")]
    public List<ErrorDetailDto>? Errors { get; init; }
}

internal sealed class ErrorDetailDto
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("source")]
    public ErrorSourceDto? Source { get; init; }
}

internal sealed class ErrorSourceDto
{
    [JsonPropertyName("parameter")]
    public string? Parameter { get; init; }

    [JsonPropertyName("example")]
    public string? Example { get; init; }
}

/// <summary>Wire shape of an OAuth 2.0 error response.</summary>
internal sealed class OAuthErrorDto
{
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; init; }
}
