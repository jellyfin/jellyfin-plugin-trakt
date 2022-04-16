using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users.Collection
{
    /// <summary>
    /// The trakt.tv users metadata class.
    /// </summary>
    public class TraktMetadata
    {
        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        [JsonPropertyName("media_type")]
        public string MediaType { get; set; }

        /// <summary>
        /// Gets or sets the resolution.
        /// </summary>
        [JsonPropertyName("resolution")]
        public string Resolution { get; set; }

        /// <summary>
        /// Gets or sets the audio.
        /// </summary>
        [JsonPropertyName("audio")]
        public string Audio { get; set; }

        /// <summary>
        /// Gets or sets the amount of audio channels.
        /// </summary>
        [JsonPropertyName("audio_channels")]
        public string AudioChannels { get; set; }

        // public bool 3d { get; set; }
    }
}
