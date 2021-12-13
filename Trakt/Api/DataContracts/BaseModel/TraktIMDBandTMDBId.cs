namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktIMDBandTMDBId : TraktId
    {
        public string imdb { get; set; }

        public int? tmdb { get; set; }
    }
}