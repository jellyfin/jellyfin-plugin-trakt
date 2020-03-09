using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings
{
    public class TraktMovieRated : TraktRated
    {
        public TraktMovie movie { get; set; }
    }
}