using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Comments
{
    public class TraktComment
    {
        public int id { get; set; }

        public int? parent_id { get; set; }

        public string created_at { get; set; }

        public string comment { get; set; }

        public bool spoiler { get; set; }

        public bool review { get; set; }

        public int replies { get; set; }

        public int likes { get; set; }

        public int? user_rating { get; set; }

        public TraktUserSummary user { get; set; }
    }
}