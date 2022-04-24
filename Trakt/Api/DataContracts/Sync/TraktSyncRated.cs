using Trakt.Api.DataContracts.Sync.Ratings;

namespace Trakt.Api.DataContracts.Sync;

/// <summary>
/// The trakt.tv sync rated class.
/// </summary>
public class TraktSyncRated : TraktSync<TraktMovieRated, TraktShowRated, TraktEpisodeRated>
{
}
