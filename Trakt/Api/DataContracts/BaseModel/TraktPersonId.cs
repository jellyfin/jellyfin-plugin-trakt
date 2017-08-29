
namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktPersonId : TraktId
    {
        public string imdb { get; set; }

        public int? tmdb { get; set; }

        public int? tvrage { get; set; }
    }
}