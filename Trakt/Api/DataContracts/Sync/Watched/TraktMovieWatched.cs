using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched
{
    public class TraktMovieWatched : TraktMovie
    {
        public string watched_at { get; set; }
    }
}