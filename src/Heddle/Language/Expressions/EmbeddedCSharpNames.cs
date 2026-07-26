namespace Heddle.Language.Expressions
{
    /// <summary>
    /// <para>The identifiers a user's embedded C# expression may bind. The dynamic tier declares them as the
    /// parameter list of the generated <c>ProcessData_*</c>/<c>PreProcessData</c> method in the two embedded
    /// <c>.tcs</c> templates; the emitter declares the model one as a local and refuses expressions naming the
    /// other two.</para>
    /// <para>The <c>.tcs</c> side is literal template text, so these consts cannot flow into it — a pin test
    /// (<c>EmbeddedCSharpNamesPinTests</c>) reads both embedded resources instead and asserts their parameter
    /// lists match exactly these three, in this order. Renaming a <c>.tcs</c> parameter silently changes what a
    /// pasted C# expression means on the dynamic tier only; the pin is the tripwire.</para>
    /// </summary>
    internal static class EmbeddedCSharpNames
    {
        /// <summary>The current model — the only one the precompiled tier can reproduce today.</summary>
        internal const string Model = "model";

        /// <summary>The chained-channel value.</summary>
        internal const string Chained = "chained";

        /// <summary>The root model.</summary>
        internal const string Root = "root";
    }
}
