using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Collection
{
    public class TraktEpisodeCollected : TraktEpisode
    {
        public string collected_at { get; set; }

        public string media_type { get; set; }

        public string resolution { get; set; }

        public string audio { get; set; }

        public string audio_channels { get; set; }

        //public bool 3d { get; set; }
    }
}