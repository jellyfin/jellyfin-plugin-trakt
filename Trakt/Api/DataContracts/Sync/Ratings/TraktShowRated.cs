#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Ratings
{
    /// <summary>
    /// The trakt.tv sync show rated class.
    /// </summary>
    public class TraktShowRated : TraktRated
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the ids.
        /// </summary>
        [JsonPropertyName("ids")]
        public TraktShowId Ids { get; set; }

        /// <summary>
        /// Gets or sets the seasons.
        /// </summary>
        [JsonPropertyName("seasons")]
        public IReadOnlyList<TraktSeasonRated> Seasons { get; set; }
    }
}
