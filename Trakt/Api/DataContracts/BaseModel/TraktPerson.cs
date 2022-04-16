using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel
{
    /// <summary>
    /// The trakt.tv person class.
    /// </summary>
    public class TraktPerson
    {
        /// <summary>
        /// Gets or sets the person name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the person ids.
        /// </summary>
        [JsonPropertyName("ids")]
        public TraktPersonId Ids { get; set; }
    }
}
