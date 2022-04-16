using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Scrobble
{
    /// <summary>
    /// The trakt.tv social media class.
    /// </summary>
    public class SocialMedia
    {
        /// <summary>
        /// Gets or sets a value indicating whether facebook posting should be enabled.
        /// </summary>
        [JsonPropertyName("facebook")]
        public bool Facebook { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether twittwe posting should be enabled.
        /// </summary>
        [JsonPropertyName("twitter")]
        public bool Twitter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tumblr posting should be enabled.
        /// </summary>
        [JsonPropertyName("tumblr")]
        public bool Tumblr { get; set; }
    }
}
