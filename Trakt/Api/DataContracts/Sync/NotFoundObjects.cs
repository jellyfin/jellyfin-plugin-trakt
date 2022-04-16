#pragma warning disable CA1002
#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync
{
    /// <summary>
    /// The trakt.tv sync not found objects class.
    /// </summary>
    public class NotFoundObjects
    {
        /// <summary>
        /// Gets or sets the movies.
        /// </summary>
        [JsonPropertyName("movies")]
        public List<TraktMovie> Movies { get; set; }

        /// <summary>
        /// Gets or sets the shows.
        /// </summary>
        [JsonPropertyName("shows")]
        public List<TraktShow> Shows { get; set; }

        /// <summary>
        /// Gets or sets the episodes.
        /// </summary>
        [JsonPropertyName("episodes")]
        public List<TraktEpisode> Episodes { get; set; }

        /// <summary>
        /// Gets or sets the seasons.
        /// </summary>
        [JsonPropertyName("seasons")]
        public List<TraktSeason> Seasons { get; set; }

        /// <summary>
        /// Gets or sets the people.
        /// </summary>
        [JsonPropertyName("people")]
        public List<TraktPerson> People { get; set; }
    }
}
