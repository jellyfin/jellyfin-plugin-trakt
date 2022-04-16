using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts
{
    /// <summary>
    /// The trakt.tv user access token class.
    /// </summary>
    public class TraktUserAccessToken
    {
        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the token type.
        /// </summary>
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        /// <summary>
        /// Gets or sets the expiration.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the refresh token.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the scope.
        /// </summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        [JsonPropertyName("created_at")]
        public int CreatedAt { get; set; }

        /// <summary>
        /// Gets the expiration time.
        /// </summary>
        // Expiration can be a bit of a problem with Trakt. It's usually 90 days, but it's unclear when the
        // refresh_token expires, so leave a little buffer for expiration...
        [JsonPropertyName("expirationWithBuffer")]
        public int ExpirationWithBuffer => ExpiresIn * 3 / 4;
    }
}
