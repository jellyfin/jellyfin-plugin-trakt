
namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktTVId : TraktIMDBandTMDBId
    {
        public int? tvdb { get; set; }

        public int? tvrage { get; set; }
    }
}