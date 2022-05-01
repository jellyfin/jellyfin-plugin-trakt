using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

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

    public Guid? CurrentSeriesId { get; set; }

    public ICollection<Movie> SeenMovies { get; set; }

    public ICollection<Movie> UnSeenMovies { get; set; }

    public ICollection<Episode> SeenEpisodes { get; set; }

    public ICollection<Episode> UnSeenEpisodes { get; set; }
}
