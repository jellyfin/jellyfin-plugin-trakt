
namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktMovie
    {
        public string title { get; set; }

        public int? year { get; set; }

        public TraktMovieId ids { get; set; }
    }
}