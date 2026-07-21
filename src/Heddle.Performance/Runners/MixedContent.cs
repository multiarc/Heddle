using System.Collections.Generic;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Shared, engine-neutral model for the mixed-page workload (cross-stack phase 1 WI1): a
    /// realistic mid-size page — literal HTML skeleton + scalar substitutions + a modest 36-row
    /// product loop + page-level and row-level conditionals. All values are ASCII with no
    /// <c>&amp; &lt; &gt; " '</c> (raw-suite rule), and every per-engine view is materialized once
    /// (static), so no benchmark op re-allocates the model.
    /// </summary>
    internal static class MixedContent
    {
        public sealed class MixedModel
        {
            public string PageTitle { get; set; }
            public string StoreName { get; set; }
            public string HeroHeading { get; set; }
            public string HeroTagline { get; set; }
            public bool ShowBanner { get; set; }
            public string BannerText { get; set; }
            public bool ShowDebugPanel { get; set; }
            public string FooterNote { get; set; }
            public int Year { get; set; }
            public string SupportEmail { get; set; }
            public List<MixedProduct> Products { get; set; }
        }

        public sealed class MixedProduct
        {
            public string Name { get; set; }
            public string Sku { get; set; }
            public int Price { get; set; }
            public bool OnSale { get; set; }
            public string Blurb { get; set; }
        }

        public const int ProductCount = 36;

        private static readonly MixedModel Shared = BuildModel();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        private static MixedModel BuildModel()
        {
            var products = new List<MixedProduct>(ProductCount);
            for (var i = 1; i <= ProductCount; i++)
                products.Add(new MixedProduct
                {
                    Name = $"Product {i:D2}",
                    Sku = $"MX-{1000 + i}",
                    Price = 950 + i * 7,
                    OnSale = i % 3 == 0,
                    Blurb = $"A dependable workshop staple from batch {i}, checked for daily use and backed by our lifetime guarantee.",
                });
            return new MixedModel
            {
                PageTitle = "Mercantile - Catalog",
                StoreName = "Mercantile",
                HeroHeading = "Autumn hardware sale",
                HeroTagline = "Hand-picked tools, fair prices, shipped tomorrow.",
                ShowBanner = true,
                BannerText = "Free shipping on orders over 60.",
                ShowDebugPanel = false,
                FooterNote = "Prices include VAT where applicable.",
                Year = 2026,
                SupportEmail = "support at mercantile.example",
                Products = products,
            };
        }

        private static Hash BuildDotLiquid()
        {
            var products = new List<Hash>(ProductCount);
            foreach (var p in Shared.Products)
                products.Add(new Hash
                {
                    ["name"] = p.Name,
                    ["sku"] = p.Sku,
                    ["price"] = p.Price,
                    ["on_sale"] = p.OnSale,
                    ["blurb"] = p.Blurb,
                });
            return new Hash
            {
                ["page_title"] = Shared.PageTitle,
                ["store_name"] = Shared.StoreName,
                ["hero_heading"] = Shared.HeroHeading,
                ["hero_tagline"] = Shared.HeroTagline,
                ["show_banner"] = Shared.ShowBanner,
                ["banner_text"] = Shared.BannerText,
                ["show_debug_panel"] = Shared.ShowDebugPanel,
                ["footer_note"] = Shared.FooterNote,
                ["year"] = Shared.Year,
                ["support_email"] = Shared.SupportEmail,
                ["products"] = products,
            };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var products = new List<Dictionary<string, object>>(ProductCount);
            foreach (var p in Shared.Products)
                products.Add(new Dictionary<string, object>
                {
                    ["name"] = p.Name,
                    ["sku"] = p.Sku,
                    ["price"] = p.Price,
                    ["on_sale"] = p.OnSale,
                    ["blurb"] = p.Blurb,
                });
            return new Dictionary<string, object>
            {
                ["page_title"] = Shared.PageTitle,
                ["store_name"] = Shared.StoreName,
                ["hero_heading"] = Shared.HeroHeading,
                ["hero_tagline"] = Shared.HeroTagline,
                ["show_banner"] = Shared.ShowBanner,
                ["banner_text"] = Shared.BannerText,
                ["show_debug_panel"] = Shared.ShowDebugPanel,
                ["footer_note"] = Shared.FooterNote,
                ["year"] = Shared.Year,
                ["support_email"] = Shared.SupportEmail,
                ["products"] = products,
            };
        }

        /// <summary>The Heddle-typed model (the oracle's compile context binds to it).</summary>
        public static MixedModel Model() => Shared;

        /// <summary>DotLiquid view: a <see cref="Hash"/> whose <c>products</c> is a list of row hashes.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>Lowercase snake_case dictionary view for the Fluid/Scriban twins.</summary>
        public static Dictionary<string, object> LiquidModel() => SharedDictionary;

        /// <summary>Lowercase snake_case dictionary view for the Handlebars twin.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedDictionary;
    }
}
