namespace Trakt.Api.DataContracts.SelfService;

/// <summary>
/// Preference update payload for PUT /Trakt/me/Settings.
/// </summary>
public class TraktUserSettingsUpdateDto
{
    /// <summary>
    /// Gets or sets a value indicating whether the skip unwatched import option is enabled.
    /// </summary>
    public bool SkipUnwatchedImportFromTrakt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip playback progress import option is enabled.
    /// </summary>
    public bool SkipPlaybackProgressImportFromTrakt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip watched import option is enabled.
    /// </summary>
    public bool SkipWatchedImportFromTrakt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether watched history should be posted.
    /// </summary>
    public bool PostWatchedHistory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether unwatched history should be posted.
    /// </summary>
    public bool PostUnwatchedHistory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether setting watched should be posted.
    /// </summary>
    public bool PostSetWatched { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether setting unwatched should be posted.
    /// </summary>
    public bool PostSetUnwatched { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether media info should be exported.
    /// </summary>
    public bool ExportMediaInfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether collections should be synchronized.
    /// </summary>
    public bool SynchronizeCollections { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether scrobbling is enabled.
    /// </summary>
    public bool Scrobble { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether deleted items should stay in the Trakt collection.
    /// </summary>
    public bool DontRemoveItemFromTrakt { get; set; }
}
