using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts;

public class TraktUserRefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; }

    [JsonPropertyName("redirect_uri")]
    public string RedirectUri { get; set; }

    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; }
}
