using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Scrobble
{
    public class TraktScrobbleResponse
    {
        public string action { get; set; }

        public float progress { get; set; }

        public SocialMedia sharing { get; set; }

        public class SocialMedia
        {
            public bool facebook { get; set; }

            public bool twitter { get; set; }

            public bool tumblr { get; set; }
        }

        public TraktMovie movie { get; set; }

        public TraktEpisode episode { get; set; }

        public TraktShow show { get; set; }
    }
}