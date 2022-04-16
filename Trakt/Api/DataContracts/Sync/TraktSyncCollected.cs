using Trakt.Api.DataContracts.Sync.Collection;

namespace Trakt.Api.DataContracts.Sync
{
    /// <summary>
    /// The trakt.tv sync collected class.
    /// </summary>
    public class TraktSyncCollected : TraktSync<TraktMovieCollected, TraktShowCollected, TraktEpisodeCollected>
    {
    }
}
