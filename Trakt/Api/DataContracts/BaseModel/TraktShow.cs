namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktShow
    {
        public string title { get; set; }

        public int? year { get; set; }

        public TraktShowId ids { get; set; }
    }
}