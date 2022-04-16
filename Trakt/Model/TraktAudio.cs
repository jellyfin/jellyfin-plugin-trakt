#pragma warning disable SA1300
#pragma warning disable CA1707

namespace Trakt.Model
{
    /// <summary>
    /// Enum TraktAudio.
    /// </summary>
    public enum TraktAudio
    {
        /// <summary>
        /// LPCM audio.
        /// </summary>
        lpcm,

        /// <summary>
        /// MP3 audio.
        /// </summary>
        mp3,

        /// <summary>
        /// AAC audio.
        /// </summary>
        aac,

        /// <summary>
        /// DTS audio.
        /// </summary>
        dts,

        /// <summary>
        /// DTS-HD Master Audio audio.
        /// </summary>
        dts_ma,

        /// <summary>
        /// FLAC audio.
        /// </summary>
        flac,

        /// <summary>
        /// OGG audio.
        /// </summary>
        ogg,

        /// <summary>
        /// WMA audio.
        /// </summary>
        wma,

        /// <summary>
        /// Dolby ProLogic audio.
        /// </summary>
        dolby_prologic,

        /// <summary>
        /// Dolby Digital audio.
        /// </summary>
        dolby_digital,

        /// <summary>
        /// Dolby Digital Plus audio.
        /// </summary>
        dolby_digital_plus,

        /// <summary>
        /// Dolby TrueHD audio.
        /// </summary>
        dolby_truehd
    }
}
