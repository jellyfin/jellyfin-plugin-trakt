using System.Collections.Generic;
using Trakt.Api.DataContracts.Sync.Collection;
using Trakt.Api.DataContracts.Sync.Ratings;
using Trakt.Api.DataContracts.Sync.Watched;

namespace Trakt.Api.DataContracts.Sync
{
    public class TraktSync<TMovie, TShow, TEpisode>
    {
        public List<TMovie> movies { get; set; }

        public List<TShow> shows { get; set; }

        public List<TEpisode> episodes { get; set; }
    }

    public class TraktSyncRated : TraktSync<TraktMovieRated, TraktShowRated, TraktEpisodeRated>
    {
    }

    public class TraktSyncWatched : TraktSync<TraktMovieWatched, TraktShowWatched, TraktEpisodeWatched>
    {
    }

    public class TraktSyncCollected : TraktSync<TraktMovieCollected, TraktShowCollected, TraktEpisodeCollected>
    {
    }
}