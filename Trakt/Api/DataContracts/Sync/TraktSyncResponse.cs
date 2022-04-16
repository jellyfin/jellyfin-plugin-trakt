using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync
{
    /// <summary>
    /// The trakt.tv sync response class.
    /// </summary>
    public class TraktSyncResponse
    {
        /// <summary>
        /// Gets or sets the added items.
        /// </summary>
        [JsonPropertyName("added")]
        public Items Added { get; set; }

        /// <summary>
        /// Gets or sets the deleted items.
        /// </summary>
        [JsonPropertyName("deleted")]
        public Items Deleted { get; set; }

        /// <summary>
        /// Gets or sets the existing items.
        /// </summary>
        [JsonPropertyName("existing")]
        public Items Existing { get; set; }

        /// <summary>
        /// Gets or sets the not found items.
        /// </summary>
        [JsonPropertyName("not_found")]
        public NotFoundObjects NotFound { get; set; }
    }
}
