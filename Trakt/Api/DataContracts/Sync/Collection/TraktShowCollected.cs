#pragma warning disable CA1002
#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Collection
{
    /// <summary>
    /// The trakt.tv sync show collected class.
    /// </summary>
    public class TraktShowCollected : TraktShow
    {
        /// <summary>
        /// Gets or sets the seasons.
        /// </summary>
        [JsonPropertyName("seasons")]
        public List<TraktSeasonCollected> Seasons { get; set; }
    }
}
