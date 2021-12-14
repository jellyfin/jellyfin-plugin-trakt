using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using Trakt.Model;

namespace Trakt.Helpers;

/// <summary>
/// Class that contains all the items to be reported to trakt.tv and supporting properties.
/// </summary>
internal class UserDataPackage
{
    public UserDataPackage()
    {
        SeenMovies = new List<Movie>();
        UnSeenMovies = new List<Movie>();
        SeenEpisodes = new List<Episode>();
        UnSeenEpisodes = new List<Episode>();
    }

    public TraktUser TraktUser { get; set; }

    public Guid CurrentSeriesId { get; set; }

    public List<Movie> SeenMovies { get; set; }

    public List<Movie> UnSeenMovies { get; set; }

    public List<Episode> SeenEpisodes { get; set; }

    public List<Episode> UnSeenEpisodes { get; set; }
}
