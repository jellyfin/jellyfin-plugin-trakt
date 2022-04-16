using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel
{
    /// <summary>
    /// The trakt.tv person id class.
    /// </summary>
    public class TraktPersonId : TraktIMDBandTMDBId
    {
        /// <summary>
        /// Gets or sets the TVRage person id.
        /// </summary>
        [JsonPropertyName("tvrage")]
        public int? Tvrage { get; set; }
    }
}
