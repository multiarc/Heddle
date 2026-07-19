using System.Collections.Generic;
using System.Text.RegularExpressions;
using Heddle.Performance.TestSuite.Extensions;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Shared, engine-neutral source of truth for the D1 competitor benchmark twins.
    ///
    /// The home page rendered by <see cref="TextRenderBenchmarks"/> (via <c>home.heddle</c> +
    /// <c>layout.heddle</c>) is fully static — the model is an empty <c>object</c> and every Heddle
    /// extension returns a fixed string. Under the current engine, Heddle's rendered output is the
    /// ordered concatenation of the layout's reusable-section defaults and its component/area calls
    /// (see Runners/README.md for the exact sequence and the fidelity notes). A faithful twin only
    /// has to reproduce those bytes, so the fragments live here once and every twin renders them
    /// through its own idiomatic constructs (sections, a partial/include for the layout, function/
    /// lookup component calls, and a <c>for</c>/<c>each</c> loop over the ordered area names).
    ///
    /// The area menus come straight from <see cref="AreaComponent.Areas"/> — the very dictionary
    /// Heddle renders from — so a twin cannot silently drift. Parity is then enforced by
    /// <see cref="Normalize"/> in each benchmark's GlobalSetup rather than by transcription.
    /// </summary>
    internal static class TwinContent
    {
        // ---- Fixed no-argument component outputs (mirror the Heddle extensions one-for-one) ----
        public const string CompAssetsStyles   = "<link rel=\"stylesheet\" href=\"/main.css\" />";
        public const string CompCustomStyles   = "/* CSS Comment Test */";
        public const string CompHeadScripts    = "<script src=\"/head.js\"></script>";
        public const string CompBodyScripts    = "<script src=\"/body.js\"></script>";
        public const string CompAssetsScripts  = "<script src=\"/main.js\"></script>";
        public const string CompBodyEndScripts = "<script src=\"/bodyend.js\"></script>";

        // ---- Reusable-section defaults (layout.heddle's @% ... %@ block) ----------------------
        public const string SectionMeta   = "<title>Title</title>";
        public const string SectionSocial =
            "<meta property=\"og:image\" content=\"/files/catalog/img.jpg\">" +
            "<meta itemprop=\"image\" content=\"/files/catalog/img.jpg\"/>" +
            "<link rel=\"image_src\" href=\"/files/catalog/img.jpg\"/>";
        public const string SectionPageScripts = "";
        public const string SectionEndPageScripts = "";

        /// <summary>
        /// The area-component calls in the exact order layout.heddle issues them. Heddle renders the
        /// six navigation areas, then the (empty) body section, then the footer area consecutively;
        /// because the body is empty they collapse to this single ordered list, which every twin
        /// walks with a real loop.
        /// </summary>
        public static readonly string[] AreaOrder =
        {
            "Alert Top Section Above Nav",
            "Secondary Wholesale Menu",
            "Secondary Retail Menu",
            "Wholesale Top Mega Menu",
            "Retail Top Mega Menu",
            "Alert Top Section Below Nav", // empty content
            "Footer Links",
        };

        /// <summary>Fixed no-argument component outputs, keyed by the name used in templates.</summary>
        public static Dictionary<string, string> Components() => new Dictionary<string, string>
        {
            ["assets_styles"]    = CompAssetsStyles,
            ["custom_styles"]    = CompCustomStyles,
            ["head_scripts"]     = CompHeadScripts,
            ["body_scripts"]     = CompBodyScripts,
            ["assets_scripts"]   = CompAssetsScripts,
            ["body_end_scripts"] = CompBodyEndScripts,
        };

        /// <summary>Reusable-section values passed to each twin's layout.</summary>
        public static Dictionary<string, string> Sections() => new Dictionary<string, string>
        {
            ["meta"]            = SectionMeta,
            ["social"]          = SectionSocial,
            ["page_scripts"]    = SectionPageScripts,
            ["endpage_scripts"] = SectionEndPageScripts,
        };

        /// <summary>The exact area fragments Heddle renders — reused verbatim so twins cannot drift.</summary>
        public static IReadOnlyDictionary<string, string> Areas => AreaComponent.Areas;

        /// <summary>
        /// The single documented parity normalization: collapse every run of whitespace that sits
        /// between two tags (<c>&gt; ... &lt;</c>) to nothing. Line endings are unified to <c>\n</c>
        /// first and the result is trimmed, so only genuine tag/text differences can survive.
        /// </summary>
        private static readonly Regex BetweenTags = new Regex(">\\s+<", RegexOptions.Compiled);

        public static string Normalize(string html)
        {
            if (html == null) return string.Empty;
            html = html.Replace("\r\n", "\n").Replace("\r", "\n");
            html = BetweenTags.Replace(html, "><");
            return html.Trim();
        }
    }
}
