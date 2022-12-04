using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Sync.History;
using Trakt.Api.DataContracts.Users.Collection;
using Trakt.Api.DataContracts.Users.Playback;
using Trakt.Api.DataContracts.Users.Watched;
using Trakt.Api.Enums;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Trakt;

/// <summary>
/// Class for trakt.tv plugin extension functions.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Minimum height for 576p videos.
    /// </summary>
    /// <remarks>
    /// 500px is chosen to catch all videos larger than 480px with 20px deviation.
    /// </remarks>
    private const int MinHeight576P = 500;

    /// <summary>
    /// Minimum width for 576p videos.
    /// </summary>
    /// <remarks>
    /// 630px is chosen to accomodate weird old videos, officially the lowest width for 4:3 576p video is 704px.
    /// </remarks>
    private const int MinWidth576P = 630;

    /// <summary>
    /// Minimum width for 720p videos.
    /// </summary>
    /// <remarks>
    /// 950px is chosen to accomodate 4:3 videos and 10px deviation.
    /// </remarks>
    private const int MinWidth720P = 950;

    /// <summary>
    /// Minimum width for 1080p videos.
    /// </summary>
    /// <remarks>
    /// 1400px is chosen to accomodate 4:3 videos and 40px deviation.
    /// </remarks>
    private const int MinWidth1080P = 1400;

    /// <summary>
    /// Minimum width for 2160p videos.
    /// </summary>
    /// <remarks>
    /// Includes 40px deviation.
    /// </remarks>
    private const int MinWidth2160P = 3800;

    /// <summary>
    /// Convert string to int.
    /// </summary>
    /// <param name="input">String to convert to int.</param>
    /// <returns>int?.</returns>
    public static int? ConvertToInt(this string input)
    {
        if (int.TryParse(input, out int result))
        {
            return result;
        }

        return null;
    }

    /// <summary>
    /// Checks if <see cref="TraktMetadata"/> is empty.
    /// </summary>
    /// <param name="metadata">String to convert to int.</param>
    /// <returns><see cref="bool"/> indicating if the provided <see cref="TraktMetadata"/> is empty.</returns>
    public static bool IsEmpty(this TraktMetadata metadata)
        => metadata.MediaType == null
           && metadata.Resolution == null
           && metadata.Audio == null
           && metadata.Hdr == null
           && string.IsNullOrEmpty(metadata.AudioChannels);

    /// <summary>
    /// Gets the trakt.tv codec representation of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="audioStream">The <see cref="MediaStream"/>.</param>
    /// <returns>TraktAudio.</returns>
    public static TraktAudio? GetCodecRepresetation(this MediaStream audioStream)
    {
        var audio = audioStream != null && !string.IsNullOrEmpty(audioStream.Codec)
            ? audioStream.Codec.ToLowerInvariant().Replace(' ', '_')
            : null;
        switch (audio)
        {
            case "aac":
                return TraktAudio.aac;
            case "ac3":
                return TraktAudio.dolby_digital;
            case "dca":
            case "dts":
                return TraktAudio.dts;
            case "eac3":
                return TraktAudio.dolby_digital_plus;
            case "flac":
                return TraktAudio.flac;
            case "mp2":
                return TraktAudio.mp2;
            case "mp3":
                return TraktAudio.mp3;
            case "ogg":
            case "vorbis":
                return TraktAudio.ogg;
            case "opus":
                return TraktAudio.ogg_opus;
            case "pcm":
                return TraktAudio.lpcm;
            case "truehd":
                return TraktAudio.dolby_truehd;
            case "wma":
            case "wmav2":
            case "wmapro":
            case "wmavoice":
                return TraktAudio.wma;
            default:
                return null;
        }
    }

    /// <summary>
    /// Checks if metadata of new collected movie is different from the already collected.
    /// </summary>
    /// <param name="collectedMovie">The <see cref="TraktMovieCollected"/>.</param>
    /// <param name="movie">The <see cref="Movie"/>.</param>
    /// <returns><see cref="bool"/> indicating if the new movie has different metadata to the already collected.</returns>
    public static bool MetadataIsDifferent(this TraktMovieCollected collectedMovie, Movie movie)
    {
        var match = false;
        var mediaStreams = movie.GetMediaStreams();
        var defaultVideoStream = mediaStreams.FirstOrDefault(x => x.Index == movie.DefaultVideoStreamIndex);
        var audioStream = mediaStreams.FirstOrDefault(x => x.Type == MediaStreamType.Audio);

        if (defaultVideoStream != null)
        {
            var is3D = movie.Is3D;
            var resolution = defaultVideoStream.GetResolution();
            var hdr = defaultVideoStream.GetHdr();
            match = match || collectedMovie.Metadata.Resolution != resolution || collectedMovie.Metadata.Is3D != is3D || collectedMovie.Metadata.Hdr != hdr;
        }

        if (audioStream != null)
        {
            var audio = GetCodecRepresetation(audioStream);
            var audioChannels = audioStream.GetAudioChannels();
            match = match || collectedMovie.Metadata.Audio != audio || collectedMovie.Metadata.AudioChannels != audioChannels;
        }

        return match || collectedMovie.Metadata.MediaType != TraktMediaType.digital;
    }

    /// <summary>
    /// Checks if metadata of new collected episode is different from the already collected.
    /// </summary>
    /// <param name="collectedEpisode">The <see cref="TraktEpisodeCollected"/>.</param>
    /// <param name="episode">The <see cref="Episode"/>.</param>
    /// <returns><see cref="bool"/> indicating if the new episode has different metadata to the already collected.</returns>
    public static bool MetadataIsDifferent(this TraktEpisodeCollected collectedEpisode, Episode episode)
    {
        var match = false;
        var mediaStreams = episode.GetMediaStreams();
        var defaultVideoStream = mediaStreams.FirstOrDefault(x => x.Index == episode.DefaultVideoStreamIndex);
        var audioStream = mediaStreams.FirstOrDefault(x => x.Type == MediaStreamType.Audio);

        if (defaultVideoStream != null)
        {
            var is3D = episode.Is3D;
            var resolution = defaultVideoStream.GetResolution();
            var hdr = defaultVideoStream.GetHdr();
            match = match || collectedEpisode.Metadata.Resolution != resolution || collectedEpisode.Metadata.Is3D != is3D || collectedEpisode.Metadata.Hdr != hdr;
        }

        if (audioStream != null)
        {
            var audio = GetCodecRepresetation(audioStream);
            var audioChannels = audioStream.GetAudioChannels();
            match = match || collectedEpisode.Metadata.Audio != audio || collectedEpisode.Metadata.AudioChannels != audioChannels;
        }

        return match || collectedEpisode.Metadata.MediaType != TraktMediaType.digital;
    }

    /// <summary>
    /// Gets the resolution of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="videoStream">The <see cref="MediaStream"/>.</param>
    /// <returns>string.</returns>
    public static TraktResolution? GetResolution(this MediaStream videoStream)
    {
        if (videoStream == null)
        {
            return null;
        }

        if (!videoStream.Width.HasValue)
        {
            return null;
        }

        if (videoStream.Width.Value >= MinWidth2160P)
        {
            return TraktResolution.uhd_4k;
        }

        if (videoStream.Width.Value >= MinWidth1080P)
        {
            return videoStream.IsInterlaced ? TraktResolution.hd_1080i : TraktResolution.hd_1080p;
        }

        if (videoStream.Width.Value >= MinWidth720P)
        {
            return TraktResolution.hd_720p;
        }

        if (videoStream.Width.Value >= MinWidth576P && videoStream.Height.HasValue && videoStream.Height.Value >= MinHeight576P)
        {
            return videoStream.IsInterlaced ? TraktResolution.sd_576i : TraktResolution.sd_576p;
        }

        // Set 480p as fallback since trakt.tv does not allow lower resolutions
        return videoStream.IsInterlaced ? TraktResolution.sd_480i : TraktResolution.sd_480p;
    }

    /// <summary>
    /// Gets the HDR type of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="videoStream">The <see cref="MediaStream"/>.</param>
    /// <returns>string.</returns>
    public static TraktHdr? GetHdr(this MediaStream videoStream)
    {
        if (videoStream.DvProfile != null)
        {
            return TraktHdr.dolby_vision;
        }

        var rageType = videoStream.VideoRangeType;
        switch (rageType)
        {
            case "DOVI":
                return TraktHdr.dolby_vision;
            case "HDR10":
                return TraktHdr.hdr10;
            case "HLG":
                return TraktHdr.hlg;
            default:
                return null;
        }
    }

    /// <summary>
    /// Gets the ISO-8601 representation of a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/>.</param>
    /// <returns>string.</returns>
    public static string ToISO8601(this DateTime dateTime)
        => dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the season number of an <see cref="Episode"/>.
    /// </summary>
    /// <param name="episode">The <see cref="Episode"/>.</param>
    /// <returns>int.</returns>
    public static int GetSeasonNumber(this Episode episode)
        => (episode.ParentIndexNumber != 0 ? episode.ParentIndexNumber ?? 1 : episode.ParentIndexNumber).Value;

    /// <summary>
    /// Gets the number of audio channels of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="audioStream">The <see cref="MediaStream"/>.</param>
    /// <returns>string.</returns>
    public static string GetAudioChannels(this MediaStream audioStream)
    {
        if (audioStream == null || string.IsNullOrEmpty(audioStream.ChannelLayout))
        {
            return null;
        }

        var channels = audioStream.ChannelLayout;
        switch (channels)
        {
            case "octagonal":
            case "7.1(wide-side)":
            case "7.1(wide)":
            case "7.1":
                return "7.1";
            case "7.0(front)":
            case "7.0":
            case "6.1(back)":
            case "6.1(front)":
            case "6.1":
                return "6.1";
            case "hexagonal":
            case "6.0":
            case "6.0(front)":
            case "5.1(side)":
            case "5.1":
                return "5.1";
            case "5.0(side)":
            case "5.0":
                return "5.0";
            case "4.1":
                return "4.1";
            case "quad(side)":
            case "quad":
            case "4.0":
                return "4.0";
            case "3.1":
                return "3.1";
            case "3.0(back)":
            case "3.0":
                return "3.0";
            case "2.1":
                return "2.1";
            case "stereo":
                return "2.0";
            case "mono":
                return "1.0";
            default:
                return null;
        }
    }

    /// <summary>
    /// Transforms an enumerable into a list with a speciifc amount of chunks.
    /// </summary>
    /// <param name="enumerable">The IEnumberable{T}.</param>
    /// <param name="chunkSize">Size of the Chunks.</param>
    /// <returns>IList{IEnumerable{T}}.</returns>
    /// <typeparam name="T">The type of IEnumerable.</typeparam>
    public static IList<IEnumerable<T>> ToChunks<T>(this IEnumerable<T> enumerable, int chunkSize)
    {
        var itemsReturned = 0;
        var list = enumerable.ToList(); // Prevent multiple execution of IEnumerable.
        var count = list.Count;
        var chunks = new List<IEnumerable<T>>();
        while (itemsReturned < count)
        {
            chunks.Add(list.Take(chunkSize).ToList());
            list = list.Skip(chunkSize).ToList();
            itemsReturned += chunkSize;
        }

        return chunks;
    }

    /// <summary>
    /// Gets a watched match for a series.
    /// </summary>
    /// <param name="item">The <see cref="Series"/>.</param>
    /// <param name="results">The <see cref="IEnumerable{TraktShowWatched}"/>.</param>
    /// <returns>TraktShowWatched.</returns>
    public static TraktShowWatched FindMatch(Series item, IEnumerable<TraktShowWatched> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Show));
    }

    /// <summary>
    /// Gets a collected match for a series.
    /// </summary>
    /// <param name="item">The <see cref="Series"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{TraktShowCollected}"/>.</param>
    /// <returns>TraktShowCollected.</returns>
    public static TraktShowCollected FindMatch(Series item, IEnumerable<TraktShowCollected> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Show));
    }

    /// <summary>
    /// Gets a paused match for a series.
    /// </summary>
    /// <param name="item">The <see cref="Episode"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{TraktShowCollected}"/>.</param>
    /// <returns>TraktShowCollected.</returns>
    public static TraktEpisodePaused FindMatch(Episode item, IEnumerable<TraktEpisodePaused> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Episode));
    }

    /// <summary>
    /// Gets a watched match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{TraktMovieWatched}"/>.</param>
    /// <returns>TraktMovieWatched.</returns>
    public static TraktMovieWatched FindMatch(BaseItem item, IEnumerable<TraktMovieWatched> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a collected match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{TraktMovieCollected}"/>.</param>
    /// <returns>TraktMovieCollected.</returns>
    public static TraktMovieCollected FindMatch(BaseItem item, IEnumerable<TraktMovieCollected> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a paused match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{TraktMoviePaused}"/>.</param>
    /// <returns>TraktMoviePaused.</returns>
    public static TraktMoviePaused FindMatch(BaseItem item, IEnumerable<TraktMoviePaused> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a watched history match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{TraktMovieWatchedHistory}"/>.</param>
    /// <returns>TraktMovieWatchedHistory.</returns>
    public static TraktMovieWatchedHistory FindMatch(Movie item, IEnumerable<TraktMovieWatchedHistory> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a watched history match for an episode.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{TraktEpisodeWatchedHistory}"/>.</param>
    /// <returns>TraktMovieWatchedHistory.</returns>
    public static TraktEpisodeWatchedHistory FindMatch(Episode item, IEnumerable<TraktEpisodeWatchedHistory> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Episode));
    }

    /// <summary>
    /// Checks if a <see cref="BaseItem"/> matches a <see cref="TraktMovie"/>.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="movie">The IEnumerable of <see cref="TraktMovie"/>.</param>
    /// <returns><see cref="bool"/> indicating if the <see cref="BaseItem"/> matches a <see cref="TraktMovie"/>.</returns>
    public static bool IsMatch(BaseItem item, TraktMovie movie)
    {
        var imdb = item.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdb) && string.Equals(imdb, movie.Ids.Imdb, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tmdb = item.GetProviderId(MetadataProvider.Tmdb);
        if (!string.IsNullOrEmpty(tmdb) && string.Equals(tmdb, movie.Ids.Tmdb.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a <see cref="Series"/> matches a <see cref="TraktShow"/>.
    /// </summary>
    /// <param name="item">The <see cref="Series"/>.</param>
    /// <param name="show">The <see cref="TraktShow"/>.</param>
    /// <returns><see cref="bool"/> indicating if the <see cref="Series"/> matches a <see cref="TraktShow"/>.</returns>
    public static bool IsMatch(Series item, TraktShow show)
    {
        var tvdb = item.GetProviderId(MetadataProvider.Tvdb);
        if (!string.IsNullOrEmpty(tvdb) && string.Equals(tvdb, show.Ids.Tvdb, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tmdb = item.GetProviderId(MetadataProvider.Tmdb);
        if (!string.IsNullOrEmpty(tmdb) && string.Equals(tmdb, show.Ids.Tmdb.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var imdb = item.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdb) && string.Equals(imdb, show.Ids.Imdb, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tvrage = item.GetProviderId(MetadataProvider.TvRage);
        if (!string.IsNullOrEmpty(tvrage) && string.Equals(tvrage, show.Ids.Tvrage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a <see cref="Episode"/> matches a <see cref="TraktEpisode"/>.
    /// </summary>
    /// <param name="item">The <see cref="Episode"/>.</param>
    /// <param name="episode">The <see cref="TraktEpisode"/>.</param>
    /// <returns><see cref="bool"/> indicating if the <see cref="Episode"/> matches a <see cref="TraktEpisode"/>.</returns>
    public static bool IsMatch(Episode item, TraktEpisode episode)
    {
        var tvdb = item.GetProviderId(MetadataProvider.Tvdb);
        if (!string.IsNullOrEmpty(tvdb) && string.Equals(tvdb, episode.Ids.Tvdb, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tmdb = item.GetProviderId(MetadataProvider.Tmdb);
        if (!string.IsNullOrEmpty(tmdb) && string.Equals(tmdb, episode.Ids.Tmdb.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var imdb = item.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdb) && string.Equals(imdb, episode.Ids.Imdb, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tvrage = item.GetProviderId(MetadataProvider.TvRage);
        if (!string.IsNullOrEmpty(tvrage) && string.Equals(tvrage, episode.Ids.Tvrage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
