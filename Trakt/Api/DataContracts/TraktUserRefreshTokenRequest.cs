using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts
{
    /// <summary>
    /// The trakt.tv user refresh token class.
    /// </summary>
    public class TraktUserRefreshTokenRequest
    {
        /// <summary>
        /// Gets or sets the refresh token.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the client id.
        /// </summary>
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret.
        /// </summary>
        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the redirect URI.
        /// </summary>
        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; }

        /// <summary>
        /// Gets or sets the grant type.
        /// </summary>
        [JsonPropertyName("grant_type")]
        public string GrantType { get; set; }
    }
}
