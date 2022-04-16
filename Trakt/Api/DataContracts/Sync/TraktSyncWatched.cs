using Trakt.Api.DataContracts.Sync.Watched;

namespace Trakt.Api.DataContracts.Sync
{
    /// <summary>
    /// The trakt.tv sync watched class.
    /// </summary>
    public class TraktSyncWatched : TraktSync<TraktMovieWatched, TraktShowWatched, TraktEpisodeWatched>
    {
    }
}
