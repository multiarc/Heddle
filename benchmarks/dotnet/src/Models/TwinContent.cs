// The composed-page fixtures every engine renders from (ledger E8).
//
// Load-bearing values: the golden corpus is Heddle's render OF THESE, so changing one changes the
// oracle every ecosystem is gated against. Change them only with a corpus re-export in the same
// commit.
//
// The types are PUBLIC, and that is the point rather than an accident: Razor's views are compiled
// at runtime into a separate assembly, so an `internal` model would be unreachable to exactly one
// engine and would force a bespoke projection for it -- a second copy of the data, free to drift.
// One public fixture, read by all six, makes drift impossible instead of merely unlikely.
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace Heddle.Benchmarks.Dotnet.Models
{
    /// <summary>
    /// Shared, engine-neutral source of truth for the composed-page twins.
    ///
    /// The page Heddle renders from <c>home.heddle</c> + <c>layout.heddle</c> is fully static — the
    /// model is an empty <c>object</c> and every Heddle extension returns a fixed string — so its
    /// output is the ordered concatenation of the layout's reusable-section defaults and its
    /// component and area calls. A faithful twin only has to reproduce those bytes, so the fragments
    /// live here once and every twin renders them through its own idiomatic constructs: sections, a
    /// partial or include for the layout, function or lookup component calls, and a <c>for</c> /
    /// <c>each</c> loop over the ordered area names.
    ///
    /// The area menus come straight from <see cref="AreaData.Areas"/> — the very dictionary Heddle
    /// renders from — so a twin cannot silently drift from the oracle by transcription error. What
    /// remains is enforced by the gate, not by hand: <see cref="Normalize"/> is the parity
    /// normalization the corpus is stored under.
    /// </summary>
    public static class TwinContent
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
        public static IReadOnlyDictionary<string, string> Areas => AreaData.Areas;

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
