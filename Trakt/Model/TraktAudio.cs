using System.ComponentModel;

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
        [Description("lpcm")]
        Lpcm,

        /// <summary>
        /// MP3 audio.
        /// </summary>
        [Description("mp3")]
        Mp3,

        /// <summary>
        /// AAC audio.
        /// </summary>
        [Description("aac")]
        Aac,

        /// <summary>
        /// DTS audio.
        /// </summary>
        [Description("dts")]
        Dts,

        /// <summary>
        /// DTS-HD Master Audio audio.
        /// </summary>
        [Description("dts_ma")]
        DtsMa,

        /// <summary>
        /// FLAC audio.
        /// </summary>
        [Description("flac")]
        Flac,

        /// <summary>
        /// OGG audio.
        /// </summary>
        [Description("ogg")]
        Ogg,

        /// <summary>
        /// WMA audio.
        /// </summary>
        [Description("wma")]
        Wma,

        /// <summary>
        /// Dolby ProLogic audio.
        /// </summary>
        [Description("dolby_prologic")]
        DolbyProLogic,

        /// <summary>
        /// Dolby Digital audio.
        /// </summary>
        [Description("dolby_digital")]
        DolbyDigital,

        /// <summary>
        /// Dolby Digital Plus audio.
        /// </summary>
        [Description("dolby_digital_plus")]
        DolbyDigitalPlus,

        /// <summary>
        /// Dolby TrueHD audio.
        /// </summary>
        [Description("dolby_truehd")]
        DolbyTrueHd
    }
}
