using System;

namespace Trakt.Model;

internal class PlaybackState
{
    public bool IsPaused { get; set; } = false;

    public long PlaybackPositionTicks { get; set; } = 0L;

    public DateTime PlaybackTime { get; set; } = DateTime.UtcNow;
}
