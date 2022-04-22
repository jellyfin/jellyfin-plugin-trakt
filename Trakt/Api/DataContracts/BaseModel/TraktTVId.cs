#nullable enable

using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel
{
    /// <summary>
    /// The trakt.tv tv id class.
    /// </summary>
    public class TraktTVId : TraktIMDBandTMDBId
    {
        /// <summary>
        /// Gets or sets the TVDb id.
        /// </summary>
        [JsonPropertyName("tvdb")]
        public string? Tvdb { get; set; }

        /// <summary>
        /// Gets or sets the TVRage id.
        /// </summary>
        [JsonPropertyName("tvrage")]
        public string? Tvrage { get; set; }
    }
}
