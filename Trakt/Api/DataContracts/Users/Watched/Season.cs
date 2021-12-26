#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users.Watched;

public class Season
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("episodes")]
    public List<Episode> Episodes { get; set; }
}
