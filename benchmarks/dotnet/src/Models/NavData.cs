// The composed-page structured navigation fixtures (ledger E20; supersedes the mega-menu and
// footer-link blobs of the since-deleted AreaData dictionary — under E22 this file is the ONLY
// composed-page model source, and every fragment of literal page text lives in the templates).
//
// Transcribed faithfully from the retired "Wholesale Top Mega Menu", "Retail Top Mega Menu" and
// "Footer Links" HTML blobs: every tab, column, section and link the blobs carried is here, in
// document order. What the blobs carried that the structure cannot express — decorative images,
// close buttons, the phone-number strip — is deliberately dropped: the workload renders the
// NAVIGATION through loops and nested partials, not the ad chrome around it.
//
// Every text value is sanitized to workloads.md rule 4 (ASCII printable, none of & < > " '):
// `&`/`&amp;` become `and`, apostrophes and the TM entity are dropped, `&eacute;` flattens to
// `e`, stray double spaces collapse. The static constructor ASSERTS the rule over every label,
// title and href, so a transcription slip fails at first touch instead of exporting a corpus
// that double-escaping engines cannot reproduce.
//
// The types are PUBLIC and top-level, and that is the point rather than an accident: Razor's
// views are compiled at runtime into a separate assembly, and the Heddle templates name
// `ComposedModel` (and bind these types per call site) by short name through the configured
// assembly namespaces — a nested type would be unreachable to both.
using System;
using System.Collections.Generic;
using DotLiquid;

namespace Heddle.Benchmarks.Dotnet.Models
{
    public sealed class NavModel
    {
        public List<MegaMenu> Menus { get; set; }
        public List<NavColumn> FooterColumns { get; set; }
    }

    public sealed class MegaMenu
    {
        public List<MenuTab> Tabs { get; set; }
    }

    public sealed class MenuTab
    {
        public string Label { get; set; }
        public string Href { get; set; }
        public string Css { get; set; }
        public bool HasDropdown { get; set; }
        public string DropdownCss { get; set; }
        public List<NavColumn> Columns { get; set; }
    }

    public sealed class NavColumn
    {
        public List<NavSection> Sections { get; set; }
    }

    public sealed class NavSection
    {
        public string Title { get; set; }
        public string Href { get; set; }
        /// <summary>Precomputed: the section title renders as a link exactly when the source blob
        /// linked it. Precomputed so no engine evaluates a string test (common-denominator rule).</summary>
        public bool TitleLinked { get; set; }
        public List<NavLink> Links { get; set; }
    }

    public sealed class NavLink
    {
        public string Label { get; set; }
        public string Href { get; set; }
    }

    /// <summary>
    /// The structured navigation every engine renders the composed-page nav from: two mega menus
    /// (wholesale, retail) and four footer columns. Exported to
    /// <c>GoldenCorpus/fixtures/composed-page/nav.json</c> for the non-.NET ports.
    /// </summary>
    public static class NavData
    {
        private static readonly NavModel Shared = Build();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        /// <summary>The Heddle-typed model view.</summary>
        public static NavModel Model() => Shared;

        /// <summary>DotLiquid view: nested <see cref="Hash"/>es, snake_case keys.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>snake_case dictionary view for the Fluid/Scriban/Handlebars twins.</summary>
        public static Dictionary<string, object> DictionaryModel() => SharedDictionary;

        // ---- construction helpers ---------------------------------------------------------------

        private static NavLink L(string label, string href) => new NavLink { Label = label, Href = href };

        /// <summary>A section whose title is linked when a non-empty href is given.</summary>
        private static NavSection S(string title, string href, params NavLink[] links) => new NavSection
        {
            Title = title,
            Href = href,
            TitleLinked = href.Length != 0,
            Links = new List<NavLink>(links),
        };

        private static NavColumn C(params NavSection[] sections)
            => new NavColumn { Sections = new List<NavSection>(sections) };

        private static MenuTab T(string label, string href, string css, params NavColumn[] columns) => new MenuTab
        {
            Label = label,
            Href = href,
            Css = css,
            HasDropdown = columns.Length != 0,
            DropdownCss = columns.Length != 0 ? "dropdown_2columns" : "",
            Columns = new List<NavColumn>(columns),
        };

        // ---- the transcription ------------------------------------------------------------------

        /// <summary>"Shop All Products" — the only tab that differs between the two menus, so the
        /// two variant rows are parameters.</summary>
        private static MenuTab ShopAllProducts(NavLink fishSausageRow, NavLink meatPorkRow) =>
            T("Shop All Products", "#", "shop-all-products",
                C(
                    S("FISH", "",
                        L("Salmon", "/products/wild-salmon"),
                        L("Halibut", "/products/wild-alaskan-halibut"),
                        L("Cod", "/products/wild-alaskan-cod"),
                        L("Sablefish", "/products/wild-alaskan-sablefish-aka-black-cod-butterfish"),
                        L("Tuna", "/products/wild-tuna"),
                        L("Smoked Fish and Lox", "/products/smoked-fish-and-lox"),
                        fishSausageRow,
                        L("Petrale Sole", "/product/wild-petrale-sole-portions-4-6-oz-1-lb-bags")),
                    S("SHELLFISH", "/products/wild-and-eco-grown-shellfish",
                        L("Scallops", "/products/wild-sea-scallops"),
                        L("Clams, Oysters and Mussels", "/products/cultured-shellfish-mussels-clams-oysters"),
                        L("Wild Shrimp", "/product/oregon-pink-shrimp-cooked"),
                        L("Spot Prawns", "/products/wild-pacific-spot-prawns"),
                        L("Crab", "/products/crab"),
                        L("Lobster", "/products/wild-maine-lobster"),
                        L("Calamari", "/product/wild-pacific-calamari-8-oz"),
                        L("See All", "/products/wild-and-eco-grown-shellfish")),
                    S("MEAT", "",
                        meatPorkRow,
                        L("Grass-Fed Bison", "/products/grass-fed-bison"),
                        L("Grass Fed Beef", "/products/organic-grass-fed-beef-by-skagit-river-ranch"),
                        L("Heritage Chicken", "/products/heritage-chicken"),
                        L("Bone Broth", "/products/bone-broth-fish-chicken-and-beef"),
                        L("Burgers and Hotdogs: Fish and Meat", "/products/burgers-and-hot-dogs"))),
                C(
                    S("CANNED AND POUCHED", "/products/canned-and-pouched-wild-seafood",
                        L("Salmon", "/products/wild-canned-sockeye-salmon"),
                        L("Sardines", "/products/canned-wild-portuguese-sardines-bone-in-and-fillets"),
                        L("Tuna", "/products/canned-and-pouched-tuna"),
                        L("Anchovies", "/products/anchovies"),
                        L("Shrimp", "/products/wild-oregon-tiny-pink-shrimp"),
                        L("Crab", "/products/canned-wild-pacific-dungeness-crab"),
                        L("Mackerel", "/products/canned-wild-portuguese-mackerel"),
                        L("Mussels", "/products/smoked-mussels-cultured"),
                        L("Canned Samplers", "/products/canned-wild-seafood-samplers"),
                        L("See All", "/products/canned-and-pouched-wild-seafood")),
                    S("ORGANIC FOODS AND SEASONINGS", "/products/organic-food-meat-and-poultry",
                        L("Broth, Soups and Meals", "/products/meals-broth-and-soups-organic-and-natural"),
                        L("Frozen Berries", "/products/organic-berries-frozen"),
                        L("Oils and Vinegars", "/products/organic-oils-and-vinegar"),
                        L("Garlic, Marinades and Seasonings", "/products/organic-marinade-rub-mixes-and-seasonings"),
                        L("Natural Jerky - Salmon and Bison", "/products/natural-jerky"),
                        L("Dried Fruit", "/products/organic-dried-fruits"),
                        L("Chocolate", "/products/organic-extra-dark-chocolate"),
                        L("Trail Mix", "/products/organic-trail-mix"),
                        L("Seaweed and Kelp", "/products/seaweed-salad-and-kelp-cubes"),
                        L("See All", "/products/organic-food-meat-and-poultry"))),
                C(
                    S("OMEGA-3s AND SUPPLEMENTS", "/products/omega-3s-herbs-tests-vitamin-d-and-astaxanthin",
                        L("Omega-3 Wild Salmon Oil", "/products/omega-3-wild-salmon-oil-supplements"),
                        L("Vitamin D3 + Omega-3 Combos", "/products/vitamin-d3-omega-3-combos"),
                        L("High DHA Brain Care Therapy", "/product/high-dha-brain-care-vitamin-d3"),
                        L("Daily Dose Packs", "/products/daily-supplement-packs"),
                        L("Omega-3 Krill Oil", "/products/omega-3-krill-oil"),
                        L("Vital Omega-3/6 Test Kit", "/products/vital-omega-3-6-hufa-test"),
                        L("Curcumin in Wild Salmon Oil", "/product/curcumin-in-wild-alaskan-salmon-oil-250mg-120-ct"),
                        L("See All", "/products/omega-3s-herbs-tests-vitamin-d-and-astaxanthin")),
                    S("SAMPLERS AND GIFT PACKS", "",
                        L("Samplers", "/products/samplers"),
                        L("Health Advisor Packs", "/products/health-advisors-packs"),
                        L("Pet Treats", "/products/pet-products"),
                        L("Healthy Mom and Baby", "/content/healthy-mom-baby"),
                        L("Doctors Favorites", "/products/doctors-favorites"),
                        L("All Gifts and Packs", "/products/gift-packs-and-certificates"),
                        L("Gift Certificates", "/products/gift-certificates")),
                    S("COOKS TOOLS AND BOOKS", "",
                        L("Gift Certificates", "/product/vital-choice-gift-certificate?cat=278"),
                        L("Cooks Tools and Books", "/products/cookbooks-and-cooking-accessories"),
                        L("Grilling Accessories", "/products/grilling-accessories"))),
                C(
                    S("SPECIAL OFFERS / VALUE PICKS", "/products/special-offers"),
                    S("NEW PRODUCTS", "/products/new-at-vital-choice"),
                    S("TOP SELLERS", "/products/top-sellers"),
                    S("RANDYS PICKS", "/products/randy-s-picks"),
                    S("GIFT CERTIFICATES", "/products/gift-certificates")));

        /// <summary>"Wild Salmon" — the last product row and the sampler-title column row differ
        /// between the menus.</summary>
        private static MenuTab WildSalmon(NavLink lastProductRow, bool withSamplersTitle)
        {
            var titles = new List<NavSection>();
            if (withSamplersTitle)
                titles.Add(S("WILD SALMON SAMPLERS", "/products/wild-salmon-samplers"));
            titles.Add(S("WILD CANNED SOCKEYE SALMON", "/products/wild-canned-sockeye-salmon"));
            titles.Add(S("SAUSAGE, BACON, AND BURGERS", "/products/wild-salmon-sausage-bacon-and-burgers"));
            titles.Add(S("SMOKED FISH AND LOX", "/products/smoked-fish-and-lox"));
            titles.Add(S("OMEGA-3 WILD SALMON OIL", "/products/omega-3-wild-salmon-oil-supplements"));

            return T("Wild Salmon", "#", "wild-salmon",
                C(
                    S("Wild Salmon", "/products/wild-salmon",
                        L("Wild Alaskan Sockeye Salmon", "/products/wild-alaskan-sockeye-salmon"),
                        L("Arctic Keta", "/products/artic-keta-salmon"),
                        L("Wild Pacific King Salmon", "/products/wild-pacific-king-salmon"),
                        L("Wild Alaskan Silver Salmon", "/products/wild-alaskan-silver-salmon"),
                        L("Wild Salmon Samplers", "/products/wild-salmon-samplers"),
                        L("Seared Sockeye Salmon (Tataki)", "/products/seared-sockeye-salmon-tataki"),
                        L("Ikura Wild Salmon Caviar", "/products/ikura-wild-salmon-caviar"),
                        lastProductRow)),
                new NavColumn { Sections = titles });
        }

        private static MenuTab Supplements() =>
            T("Supplements", "#", "supplements",
                C(
                    S("OMEGA-3S AND SUPPLEMENTS", "/products/omega-3s-herbs-tests-vitamin-d-and-astaxanthin",
                        L("Omega-3 Wild Salmon Oil", "/products/omega-3-wild-salmon-oil-supplements"),
                        L("Vitamin D3 + Omega-3 Combos", "/products/vitamin-d3-omega-3-combos"),
                        L("High DHA Brain Care Therapy", "/product/high-dha-prenatal-therapy-vitamin-d3-180-softgels"),
                        L("Daily Dose Packs", "/products/daily-supplement-packs"),
                        L("Omega-3 Krill Oil", "/products/omega-3-krill-oil"),
                        L("Vital Omega-3/6 Test Kit", "/products/vital-omega-3-6-hufa-test"),
                        L("Curcumin in Wild Salmon Oil", "/product/curcumin-in-wild-alaskan-salmon-oil-250mg-120-ct"),
                        L("See All", "/products/omega-3s-herbs-tests-vitamin-d-and-astaxanthin"))));

        private static MenuTab AboutVitalChoice() =>
            T("About Vital Choice", "#", "about-vital-choice",
                C(
                    S("WHO WE ARE: OUR STORY", "",
                        L("About Vital Choice", "/content/about-vital-choice"),
                        L("Our Mission", "/content/our-mission"),
                        L("Customer and Expert Reviews", "/content/what-are-people-saying-about-vital-choice"))),
                C(
                    S("WHY WE ARE DIFFERENT", "",
                        L("Purity Standards", "/content/purity-story"),
                        L("Vital Green Eco Programs", "/content/vital-green-environmental-stewardship-program"),
                        L("Sustainable Seafood", "/content/sustainability"))),
                C(
                    S("WHAT WE BELIEVE", "",
                        L("A Letter from Randy Hartnell", "/content/a-letter-from-randy-hartnell"),
                        L("Randys Podcasts", "/content/the-antidote-podcast-series"),
                        L("Giving Back", "/content/giving-back-to-the-community"))));

        private static MenuTab Learn() =>
            T("Learn", "#", "learn",
                C(
                    S("RECOMMENDED READING", "",
                        L("Newsletter Sign-up", "/content/newsletter-sign-up"),
                        L("Newsletter Articles Archive", "/articles/"),
                        L("FAQs", "/faqs"))),
                C(
                    S("HEALTH AND NUTRITION", "",
                        L("Take the Vital Omega-3/6 Test", "/product/the-vital-omega-3-6-hufa-test-trade"),
                        L("Omega-3 Facts", "/content/omega-3-facts-sources"),
                        L("Seafood Benefits", "/content/health-benefits-of-fish"),
                        L("Omega 3/6 Balance", "/content/omega-3-6-balance-scores-how-to-use-them"),
                        L("Healthy Mom and Baby", "/content/healthy-mom-baby"))),
                C(
                    S("ABOUT VITAL CHOICE PRODUCTS", "",
                        L("How to Prepare Seafood", "/content/cooking-tips"),
                        L("Kosher and Organic Certification", "/content/about-vital-choice#Organic"),
                        L("The Flash-Frozen Advantage", "/content/about-vital-choice#Flash-Frozen"),
                        L("Seafood Purity and Safety", "/content/purity-story"),
                        L("Superior Salmon, Naturally", "/content/about-vital-choice#Superior%20Salmon"))));

        private static MenuTab Cook() =>
            T("Cook", "#", "cook",
                C(
                    S("COOKING VIDEOS", "/recipe/vital-choice-seafood-cooking-videos"),
                    S("CELEBRITY CHEF VIDEOS", "",
                        L("Becky Selengut", "/recipes/becky-selengut"),
                        L("Myra Kornfeld", "/recipes/myra-kornfeld"),
                        L("Rebecca Katz", "/recipes/rebecca-katz"))),
                C(
                    S("RECIPES BY CATEGORY", "",
                        L("Salmon", "/recipes/wild-salmon"),
                        L("Shellfish", "/recipes/shellfish"),
                        L("Grass Fed Beef", "/recipes/grass-fed-beef"),
                        L("Chicken", "/recipes/heritage-chicken"),
                        L("See All Recipes", "/content/in-the-kitchen"))),
                C(
                    S("SEAFOOD HOW-TO VIDEOS", "",
                        L("How-to Broil Salmon", "/recipe/how-to-broil-salmon"),
                        L("How-to Saute Salmon", "/recipe/how-to-saut-salmon"),
                        L("How-to Clean Spot Prawns", "/recipe/how-to-clean-spot-prawns"),
                        L("See All Videos", "/content/in-the-kitchen")),
                    S("GUIDE: HOW-TO PREPARE SEAFOOD", "/content/cooking-tips"),
                    S("SEE ALL RECIPES AND VIDEOS", "/content/in-the-kitchen")));

        private static MegaMenu WholesaleMenu() => new MegaMenu
        {
            Tabs = new List<MenuTab>
            {
                ShopAllProducts(
                    L("Sausage, Burgers and Bacon", "/products/wild-salmon-sausage-bacon-and-burgers"),
                    L("Pork - coming soon", "/product/coming-soon-paleo-pork")),
                WildSalmon(
                    L("Wild Salmon Jerky Strips", "/products/wild-salmon-jerky-strips"),
                    withSamplersTitle: false),
                Supplements(),
                AboutVitalChoice(),
                Learn(),
                Cook(),
            }
        };

        private static MegaMenu RetailMenu() => new MegaMenu
        {
            Tabs = new List<MenuTab>
            {
                ShopAllProducts(
                    L("Sausages,Dogs, Burgers and Bacon", "/products/wild-salmon-sausage-bacon-and-burgers"),
                    L("Paleo-Friendly Pork", "/products/paleo-friendly-pork")),
                WildSalmon(
                    L("Coming Soon: Wild Salmon Dogs", "/products/wild-salmon-hot-dogs"),
                    withSamplersTitle: true),
                Supplements(),
                AboutVitalChoice(),
                Learn(),
                Cook(),
            }
        };

        private static List<NavColumn> FooterColumns() => new List<NavColumn>
        {
            C(S("Need Help?", "",
                L("Customer Service", "/content/contact-customer-service"),
                L("Request a Catalog", "/content/request-catalog"),
                L("Customer Rewards", "/content/HealthWise-Rewards-Program"),
                L("Returns and Exchanges", "/content/the-vital-choice-guarantee"),
                L("Shipping Information", "/content/shipping-information"))),
            C(S("Our Company", "",
                L("About Us", "/content/About-Vital-Choice"),
                L("Guarantee", "/content/the-vital-choice-guarantee"),
                L("Testimonials", "/content/what-are-people-saying-about-vital-choice"),
                L("Purity Standards", "/content/Purity-Story"),
                L("Sustainability Policy", "/content/Sustainability"),
                L("Vital Green", "/content/Vital-Green-Environmental-Stewardship-Program"),
                L("News Room", "/content/News-Room"),
                L("Giving Back", "/content/Giving-Back-to-the-Community"),
                L("Privacy Policy", "/content/privacy-policy"),
                L("Careers", "/content/Careers"))),
            C(S("Programs", "",
                L("Affiliate Accounts", "/content/Web-Affiliate-Program"),
                L("Customer Rewards", "/content/HealthWise-Rewards-Program"),
                L("Wholesale Accounts", "/content/wholesale-registration"))),
            C(S("Learn", "",
                L("FAQs", "/faqs"),
                L("Recipes", "/content/in-the-kitchen"),
                L("Seafood Cooking Tips", "/content/Seafood-Storage-Cooking-Tips"),
                L("Newsletter Archive", "/articles"),
                L("Healthy Mom and Baby", "/content/Healthy-Mom-Baby"),
                L("Seafood Health Benefits", "/content/Health-Benefits-of-Fish"),
                L("Omega-3 Facts and Sources", "/content/omega-3-facts-sources"))),
        };

        private static NavModel Build()
        {
            var model = new NavModel
            {
                Menus = new List<MegaMenu> { WholesaleMenu(), RetailMenu() },
                FooterColumns = FooterColumns(),
            };
            AssertRule4(model);
            return model;
        }

        // ---- rule-4 guard -----------------------------------------------------------------------

        /// <summary>Workloads.md rule 4 over every text value: ASCII printable, none of
        /// <c>&amp; &lt; &gt; " '</c>. A violation here would change the oracle for every port.</summary>
        private static void AssertRule4(NavModel model)
        {
            void Check(string value, string where)
            {
                foreach (var c in value)
                {
                    if (c < 0x20 || c > 0x7E || c == '&' || c == '<' || c == '>' || c == '"' || c == '\'')
                        throw new InvalidOperationException(
                            $"NavData rule-4 violation in {where}: '{value}' contains 0x{(int)c:X2}");
                }
            }

            void Column(NavColumn column, string where)
            {
                foreach (var s in column.Sections)
                {
                    Check(s.Title, where + " section title");
                    Check(s.Href, where + " section href");
                    foreach (var l in s.Links)
                    {
                        Check(l.Label, where + " link label");
                        Check(l.Href, where + " link href");
                    }
                }
            }

            for (var m = 0; m < model.Menus.Count; m++)
                foreach (var tab in model.Menus[m].Tabs)
                {
                    Check(tab.Label, $"menu {m} tab label");
                    Check(tab.Href, $"menu {m} tab href");
                    Check(tab.Css, $"menu {m} tab css");
                    Check(tab.DropdownCss, $"menu {m} tab dropdown css");
                    foreach (var col in tab.Columns) Column(col, $"menu {m} tab '{tab.Label}'");
                }

            foreach (var col in model.FooterColumns) Column(col, "footer");
        }

        // ---- the two flat views -----------------------------------------------------------------

        private static Hash BuildDotLiquid()
        {
            Hash Link(NavLink l) => new Hash { ["label"] = l.Label, ["href"] = l.Href };
            Hash Section(NavSection s) => new Hash
            {
                ["title"] = s.Title,
                ["href"] = s.Href,
                ["title_linked"] = s.TitleLinked,
                ["links"] = s.Links.ConvertAll(Link),
            };
            Hash Column(NavColumn c) => new Hash { ["sections"] = c.Sections.ConvertAll(Section) };
            Hash Tab(MenuTab t) => new Hash
            {
                ["label"] = t.Label,
                ["href"] = t.Href,
                ["css"] = t.Css,
                ["has_dropdown"] = t.HasDropdown,
                ["dropdown_css"] = t.DropdownCss,
                ["columns"] = t.Columns.ConvertAll(Column),
            };
            Hash Menu(MegaMenu m) => new Hash { ["tabs"] = m.Tabs.ConvertAll(Tab) };

            return new Hash
            {
                ["menus"] = Shared.Menus.ConvertAll(Menu),
                ["footer_columns"] = Shared.FooterColumns.ConvertAll(Column),
            };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            Dictionary<string, object> Link(NavLink l) => new Dictionary<string, object>
            {
                ["label"] = l.Label,
                ["href"] = l.Href,
            };
            Dictionary<string, object> Section(NavSection s) => new Dictionary<string, object>
            {
                ["title"] = s.Title,
                ["href"] = s.Href,
                ["title_linked"] = s.TitleLinked,
                ["links"] = s.Links.ConvertAll(Link),
            };
            Dictionary<string, object> Column(NavColumn c) => new Dictionary<string, object>
            {
                ["sections"] = c.Sections.ConvertAll(Section),
            };
            Dictionary<string, object> Tab(MenuTab t) => new Dictionary<string, object>
            {
                ["label"] = t.Label,
                ["href"] = t.Href,
                ["css"] = t.Css,
                ["has_dropdown"] = t.HasDropdown,
                ["dropdown_css"] = t.DropdownCss,
                ["columns"] = t.Columns.ConvertAll(Column),
            };
            Dictionary<string, object> Menu(MegaMenu m) => new Dictionary<string, object>
            {
                ["tabs"] = m.Tabs.ConvertAll(Tab),
            };

            return new Dictionary<string, object>
            {
                ["menus"] = Shared.Menus.ConvertAll(Menu),
                ["footer_columns"] = Shared.FooterColumns.ConvertAll(Column),
            };
        }
    }
}
