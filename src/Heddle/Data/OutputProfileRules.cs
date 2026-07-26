using System;

namespace Heddle.Data
{
    /// <summary>The unnamed carrier a bodiless/bodied <c>@(…)</c> resolves to. The
    /// registry name is the wire form both tiers use — the runtime asks <c>TemplateFactory</c> for it, the
    /// generator records it in the manifest binding row — so the mapping to a name lives here, once.</summary>
    public enum UnnamedCarrierKind
    {
        /// <summary>The raw empty carrier (registry name <c>""</c> → <c>EmptyExtension</c>).</summary>
        Empty,

        /// <summary>The HTML-encoding carrier (registry name <c>"html"</c> → <c>EmptyHtmlExtension</c>).</summary>
        EmptyHtml
    }

    /// <summary>The two encoding-deciding output-profile rules shared across tiers: how <c>@profile(){{…}}</c>
    /// parses and which unnamed carrier a given profile binds. Per-side plumbing (runtime mutates context
    /// lineage; emitter uses per-chain maps) follows documented conventions.</summary>
    public static class OutputProfileRules
    {
        /// <summary>The <c>text</c> spelling <see cref="TryParseProfile"/> accepts (ordinal, case-insensitive).</summary>
        public const string TextProfileName = "text";

        /// <summary>The <c>html</c> spelling <see cref="TryParseProfile"/> accepts (ordinal, case-insensitive).</summary>
        public const string HtmlProfileName = "html";

        /// <summary>The valid-values fragment both tiers' unknown-profile messages quote.</summary>
        public const string ValidProfileValues = TextProfileName + ", " + HtmlProfileName;

        /// <summary>
        /// Parses an <c>@profile(){{…}}</c> body (or a host/editor option value) into an <see cref="OutputProfile"/>:
        /// trimmed, then matched ordinal-case-insensitively against exactly <c>text</c>/<c>html</c>. Anything else
        /// — including <c>null</c> and the empty string — is <c>false</c>, which the runtime reports as
        /// <c>HED2001</c> and the generator as <c>HED7022</c>.
        /// </summary>
        public static bool TryParseProfile(string value, out OutputProfile profile)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.Equals(trimmed, TextProfileName, StringComparison.OrdinalIgnoreCase))
            {
                profile = OutputProfile.Text;
                return true;
            }

            if (string.Equals(trimmed, HtmlProfileName, StringComparison.OrdinalIgnoreCase))
            {
                profile = OutputProfile.Html;
                return true;
            }

            profile = OutputProfile.Text;
            return false;
        }

        /// <summary>
        /// Parses an <see cref="ExpressionMode"/> option value: trimmed, then matched
        /// ordinal-case-insensitively against the enum member names. Lives beside
        /// <see cref="TryParseProfile"/> because the two are always configured together, and exists so the build
        /// tier, the runtime host surface and the language server stop each carrying their own parser.
        /// </summary>
        public static bool TryParseExpressionMode(string value, out ExpressionMode mode)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.Equals(trimmed, nameof(ExpressionMode.MemberPathsOnly), StringComparison.OrdinalIgnoreCase))
            {
                mode = ExpressionMode.MemberPathsOnly;
                return true;
            }

            if (string.Equals(trimmed, nameof(ExpressionMode.Native), StringComparison.OrdinalIgnoreCase))
            {
                mode = ExpressionMode.Native;
                return true;
            }

            if (string.Equals(trimmed, nameof(ExpressionMode.FullCSharp), StringComparison.OrdinalIgnoreCase))
            {
                mode = ExpressionMode.FullCSharp;
                return true;
            }

            mode = ExpressionMode.Native;
            return false;
        }

        /// <summary>
        /// The unnamed-carrier decision: a <b>bodiless</b> <c>@(X)</c> under
        /// <see cref="OutputProfile.Html"/> binds the encoding carrier — reusing the proven
        /// <c>[EncodeOutput]</c> pipeline — and stays the raw empty carrier under <see cref="OutputProfile.Text"/>;
        /// a <b>bodied</b> <c>@(X){{…}}</c> is a raw rescoping container and is never redirected, whatever the
        /// profile.
        /// </summary>
        public static void ResolveUnnamedCarrier(OutputProfile profile, bool hasBody, out UnnamedCarrierKind kind,
            out RenderType renderType)
        {
            if (!hasBody && profile == OutputProfile.Html)
            {
                kind = UnnamedCarrierKind.EmptyHtml;
                renderType = RenderType.Encode;
                return;
            }

            kind = UnnamedCarrierKind.Empty;
            renderType = RenderType.Raw;
        }

        /// <summary>The registry name of an unnamed carrier kind — <c>"html"</c> or the empty string. This is the
        /// string <c>TemplateFactory.Create</c> resolves and the manifest binding row records, so the two tiers
        /// cannot spell it differently.</summary>
        public static string CarrierRegistryName(UnnamedCarrierKind kind)
            => kind == UnnamedCarrierKind.EmptyHtml ? HtmlProfileName : string.Empty;
    }
}
