using System.Collections.Generic;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched
{
    public class TraktSeasonWatched : TraktSeason
    {
        public string watched_at { get; set; }

        public List<TraktEpisodeWatched> episodes { get; set; }
    }
}
