
namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktUserSummary
    {
        public string username { get; set; }

        public string name { get; set; }

        public bool vip { get; set; }

        public bool @private { get; set; }
    }
}