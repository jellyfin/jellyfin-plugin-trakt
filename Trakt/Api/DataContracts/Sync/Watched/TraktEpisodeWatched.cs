using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched
{
    public class TraktEpisodeWatched : TraktEpisode
    {
        public string watched_at { get; set; }
    }
}