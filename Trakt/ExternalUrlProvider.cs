using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Trakt;

/// <summary>
/// External url provider for Trakt.
/// </summary>
public class ExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => "Trakt";

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrEmpty(imdbId))
        {
            yield break;
        }

        switch (item)
        {
            case Movie or Trailer or LiveTvProgram { IsMovie: true }:
                yield return $"https://trakt.tv/movies/{imdbId}";
                break;
            case Episode:
                yield return $"https://trakt.tv/episodes/{imdbId}";
                break;
            case Series:
                yield return $"https://trakt.tv/shows/{imdbId}";
                break;
        }
    }
}
