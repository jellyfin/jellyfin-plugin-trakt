using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings
{
    public class TraktEpisodeRated : TraktRated
    {
        public TraktEpisode episode { get; set; }
    }
}