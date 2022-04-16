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
        public int? Tvdb { get; set; }

        /// <summary>
        /// Gets or sets the TVRage id.
        /// </summary>
        [JsonPropertyName("tvrage")]
        public int? Tvrage { get; set; }
    }
}
