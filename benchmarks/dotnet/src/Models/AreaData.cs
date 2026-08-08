// The composed-page inert area fragments (ledger E8; trimmed under the E20 redesign).
//
// Four blob areas remain: Alert Top (above nav), the two secondary menus, and the pinned-empty
// Alert Below. The two mega menus and the footer links were REMOVED from this dictionary and
// re-expressed as structured data in NavData.cs — they now render through loops and nested
// partials, which is the composition dimension the workload exists to measure. The blobs that
// stay are the hybrid's inert half: pre-rendered HTML served through each engine's
// component/lookup mechanism.
//
// This dictionary is standalone public data and the Heddle extension reads FROM it. The
// dependency runs that way deliberately: were the extension to own it, every other engine would
// need a copy, and a copy is free to drift.
//
// That is not hypothetical. An earlier Razor twin carried a hand-duplicated copy of this dictionary
// and had already drifted from it unnoticed -- an empty `logo-holder` against Heddle's
// `<a href="/">` -- precisely because nothing compared the two. One public source makes that class
// of bug impossible rather than merely unlikely.
using System;
using System.Collections.Generic;

namespace Heddle.Benchmarks.Dotnet.Models
{
    /// <summary>The inert area fragments the composed-page layout renders, keyed by area name.
    /// The structured navigation lives in <see cref="NavData"/>, not here.</summary>
    public static class AreaData
    {
        /// <summary>Ordinal-keyed so lookup never depends on the current culture.</summary>
        public static readonly IReadOnlyDictionary<string, string> Areas;

        private static readonly Dictionary<string, string> Store;

        static AreaData()
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            Store = new Dictionary<string, string>(StringComparer.Ordinal);
            Store.Add("Alert Top Section Above Nav",
                "<div class=\"top-banner\" style=\"\"><a href=\"/content/shipping-information#Holidays\"><img src=\"/files/images/sitewide-alerts/xmas-shipping-alert-1.jpg\" alt=\"holiday shipping\"/></a></div>");
            Store.Add("Secondary Wholesale Menu", @"				<ul class=""hide"">
                <li>
                <a href=""#"">New</a>
                </li>
                <li>
                <a href=""#"">Signature Products</a>
                </li>
                <li>
                <a href=""/products/specials-and-top-sellers"">Special Offers</a>
                </li>
                <li>
                <a href=""/content/in-the-kitchen"">Recipes</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/in-the-kitchen"">All Recipes</a>
                </li>
                <li>
                <a href=""/recipe/Vital-Choice-Seafood-Cooking-Videos"">Recipe Videos</a>
                </li>
                <li>
                <a href=""/content/Seafood-Storage-Cooking-Tips"">Seafood Cooking Tips</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href=""/content/About-Vital-Choice"">Why Vital Choice?</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/About-Vital-Choice"">About Us</a>
                </li>
                <li>
                <a href=""/content/our-mission"">Our Mission</a>
                </li>
                <li>
                <a href=""/content/what-are-people-saying-about-vital-choice"">Testimonials</a>
                </li>
                <li>
                <a href=""/content/Giving-Back-to-the-Community"">Giving Back</a>
                </li>
                <li>
                <a href=""/content/News-Room"">News Room</a>
                </li>
                <li>
                <a href=""/content/Vital-Green-Environmental-Stewardship-Program"">Vital Green™</a>
                </li>
                <li>
                <a href=""/content/Sustainability"">Sustainability</a>
                </li>
                <li>
                <a href=""/content/Purity-Story"">Product Purity</a>
                </li>
                <li>
                <a href=""/content/The-Antidote-Podcast-Series"">Randy's Podcasts</a>
                </li>
                <li>
                <a href=""/content/HealthWise-Rewards-Program"">Customer Rewards</a>
                </li>
                <li>
                <a href=""/faqs"">Frequent Questions (FAQs)</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href=""#"">Health & Nutrition</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/omega-3-facts-sources"">Omega-3 Basics</a>
                </li>
                <li>
                <a href=""/content/Health-Benefits-of-Fish"">Seafood Benefits</a>
                </li>
                <li>
                <a href=""/content/Omega-3-6-Balance-Scores-How-to-Use-Them"">Omega-3/6 Balance</a>
                </li>
                <li>
                <a href=""/content/Healthy-Mom-Baby"">Healthy Mom & Baby</a>
                </li>
                <li>
                <a href=""/content/Purity-Story"">Seafood Purity & Safety</a>
                </li>
                <li>
                <a href=""/content/recommended-reading"">Recommended Reading</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href=""/products/wholesale"" class=""highlighted-sec-menu"">Wholesale</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/products/canned-wild-seafood"">Canned Wild Seafood</a>
                </li>
                <li>
                <a href=""/products/dietary-supplements"">Dietary Supplements</a>
                </li>
                <li>
                <a href=""/products/organic-foods"">Organic Foods</a>
                </li>
                <div class=""sub-sec-menu-separator""></div>
                <li>
                <a href=""/content/wholesale-promotions"">Wholesale Promotions</a>
                </li>
                <li>
                <a href=""/content/wholesale-faq"">FAQ</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li class=""last-child"">
                <a href=""/content/newsletter-sign-up"">Newsletter</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/newsletter-sign-up"">Sign Up</a>
                </li>
                <li>
                <a href=""/articles"">Archives</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                </ul>");
            Store.Add("Secondary Retail Menu", @"				<ul class=""hide"">
                <li>
                <a href=""#"">New</a>
                </li>
                <li>
                <a href=""#"">Signature Products</a>
                </li>
                <li>
                <a href=""/products/specials-and-top-sellers"">Special Offers</a>
                </li>
                <li>
                <a href=""/content/in-the-kitchen"">Recipes</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/in-the-kitchen"">All Recipes</a>
                </li>
                <li>
                <a href=""/recipe/Vital-Choice-Seafood-Cooking-Videos"">Recipe Videos</a>
                </li>
                <li>
                <a href=""/content/Seafood-Storage-Cooking-Tips"">Seafood Cooking Tips</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href=""/content/About-Vital-Choice"">Why Vital Choice?</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/About-Vital-Choice"">About Us</a>
                </li>
                <li>
                <a href=""/content/our-mission"">Our Mission</a>
                </li>
                <li>
                <a href=""/content/what-are-people-saying-about-vital-choice"">Testimonials</a>
                </li>
                <li>
                <a href=""/content/Giving-Back-to-the-Community"">Giving Back</a>
                </li>
                <li>
                <a href=""/content/News-Room"">News Room</a>
                </li>
                <li>
                <a href=""/content/Vital-Green-Environmental-Stewardship-Program"">Vital Green™</a>
                </li>
                <li>
                <a href=""/content/Sustainability"">Sustainability</a>
                </li>
                <li>
                <a href=""/content/Purity-Story"">Product Purity</a>
                </li>
                <li>
                <a href=""/content/The-Antidote-Podcast-Series"">Randy's Podcasts</a>
                </li>
                <li>
                <a href=""/content/HealthWise-Rewards-Program"">Customer Rewards</a>
                </li>
                <li>
                <a href=""/faqs"">Frequent Questions (FAQs)</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href=""#"">Health & Nutrition</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/omega-3-facts-sources"">Omega-3 Basics</a>
                </li>
                <li>
                <a href=""/content/Health-Benefits-of-Fish"">Seafood Benefits</a>
                </li>
                <li>
                <a href=""/content/Omega-3-6-Balance-Scores-How-to-Use-Them"">Omega-3/6 Balance</a>
                </li>
                <li>
                <a href=""/content/Healthy-Mom-Baby"">Healthy Mom & Baby</a>
                </li>
                <li>
                <a href=""/content/Purity-Story"">Seafood Purity & Safety</a>
                </li>
                <li>
                <a href=""/content/recommended-reading"">Recommended Reading</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href=""/content/request-catalog"">Catalog</a>
                </li>
                <li class=""last-child"">
                <a href=""/content/newsletter-sign-up"">Newsletter</a>
                <div class=""dropdown_1column"">
                <div class=""col_1"">
                <ul>
                <li>
                <a href=""/content/newsletter-sign-up"">Sign Up</a>
                </li>
                <li>
                <a href=""/articles"">Archives</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                </ul>");
            Store.Add("Alert Top Section Below Nav", "");
            Areas = Store;
        }
    }
}
