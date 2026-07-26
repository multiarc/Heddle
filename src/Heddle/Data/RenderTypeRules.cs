namespace Heddle.Data
{
    /// <summary>
    /// Generator plan phase 1 D11 (area 01 F4) — the <c>[EncodeOutput]</c>/<c>[NotEncode]</c> truth table, written
    /// once. The two tiers read the attributes differently (reflection with inheritance on the run tier, symbols on
    /// the build tier) but the <em>decision</em> over the resulting pair of bools is one two-line function, and it
    /// carries the same blast radius as the profile rules: a divergence here is unencoded output on one tier.
    /// </summary>
    public static class RenderTypeRules
    {
        /// <summary>
        /// The render type of an extension carrying <paramref name="hasEncodeOutput"/> / <paramref name="hasNotEncode"/>:
        /// <c>[EncodeOutput]</c> alone encodes; <c>[NotEncode]</c> vetoes it; neither is raw. Applied to the
        /// <b>inner</b> extension, before any parameter-carrier wrap (D4 carrier-transparency).
        /// </summary>
        public static RenderType Derive(bool hasEncodeOutput, bool hasNotEncode)
            => hasEncodeOutput && !hasNotEncode ? RenderType.Encode : RenderType.Raw;
    }
}
