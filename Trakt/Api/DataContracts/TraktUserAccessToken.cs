using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts;

public class TraktUserAccessToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }

    [JsonPropertyName("created_at")]
    public int CreatedAt { get; set; }

    // Expiration can be a bit of a problem with Trakt. It's usually 90 days, but it's unclear when the
    // refresh_token expires, so leave a little buffer for expiration...
    [JsonPropertyName("expirationWithBuffer")]
    public int ExpirationWithBuffer => ExpiresIn * 3 / 4;
}
