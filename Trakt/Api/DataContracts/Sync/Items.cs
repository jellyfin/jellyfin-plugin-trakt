using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync
{
    /// <summary>
    /// The trakt.tv sync items class.
    /// </summary>
    public class Items
    {
        /// <summary>
        /// Gets or sets the movies.
        /// </summary>
        [JsonPropertyName("movies")]
        public int Movies { get; set; }

        /// <summary>
        /// Gets or sets the shows.
        /// </summary>
        [JsonPropertyName("shows")]
        public int Shows { get; set; }

        /// <summary>
        /// Gets or sets the seasons.
        /// </summary>
        [JsonPropertyName("seasons")]
        public int Seasons { get; set; }

        /// <summary>
        /// Gets or sets the episodes.
        /// </summary>
        [JsonPropertyName("episodes")]
        public int Episodes { get; set; }

        /// <summary>
        /// Gets or sets the people.
        /// </summary>
        [JsonPropertyName("people")]
        public int People { get; set; }
    }
}
