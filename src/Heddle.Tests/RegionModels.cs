using System.Collections.Generic;

namespace Heddle.Tests
{
    // Top-level model types so ':: RegionArticle' etc. resolve by short name (phase 7 fixtures).

    public class RegionArticle
    {
        public string Title { get; set; }
        public int Id { get; set; }
    }

    public class RegionSpecialArticle : RegionArticle
    {
        public string Badge { get; set; }
    }

    public class RegionFeedModel
    {
        public List<RegionArticle> Articles { get; set; }
        public bool ShowHeading { get; set; }
    }
}
