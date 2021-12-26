using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktEpisode
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("season")]
    public int? Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the episode ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktEpisodeId Ids { get; set; }
}
