using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Collection
{
    /// <summary>
    /// The trakt.tv sync episodes collected class.
    /// </summary>
    public class TraktEpisodeCollected : TraktEpisode
    {
        /// <summary>
        /// Gets or sets the colletion date.
        /// </summary>
        [JsonPropertyName("collected_at")]
        public string CollectedAt { get; set; }

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
