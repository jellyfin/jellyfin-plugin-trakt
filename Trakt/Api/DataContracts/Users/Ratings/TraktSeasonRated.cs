using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings
{
    public class TraktSeasonRated : TraktRated
    {
        public TraktSeason season { get; set; }
    }
}