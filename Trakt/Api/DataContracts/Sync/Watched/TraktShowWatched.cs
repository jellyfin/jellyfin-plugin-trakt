using System.Collections.Generic;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched
{
    public class TraktShowWatched : TraktShow
    {
        public string watched_at { get; set; }

        public List<TraktSeasonWatched> seasons { get; set; }
    }
}