namespace Trakt.Model;

internal class PlaybackState
{
    public bool IsPaused { get; set; } = false;

    public long PlaybackProgress { get; set; } = 0L;
}
