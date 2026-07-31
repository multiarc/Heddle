namespace Heddle.Language.Binding
{
    /// <summary>One conformance row: <c>target.IsAssignableFrom(source)</c> must equal <see cref="Expected"/> on
    /// both tiers.</summary>
    internal struct AssignabilityRow
    {
        internal AssignabilityRow(string source, string target, bool expected, string family)
        {
            Source = source;
            Target = target;
            Expected = expected;
            Family = family;
        }

        internal string Source { get; }
        internal string Target { get; }
        internal bool Expected { get; }

        /// <summary>The row family, so a failure names what class of disagreement re-opened.</summary>
        internal string Family { get; }

        public override string ToString() => Family + ": " + Source + " -> " + Target;
    }

    /// <summary>
    /// The shared assignability conformance corpus. One data source, two drivers — a reflection-side
    /// test in <c>Heddle.Tests</c> and a symbol-side test in <c>Heddle.Generator.Tests</c> — in the
    /// <c>DefaultFunctionLockstepTests</c> mould.
    /// <para>The relation itself cannot be shared (it <em>is</em> the type graph); what this corpus pins is that
    /// the Roslyn adapter's corrections land it on the <b>CLR's</b> answer, including the two nullable rows where
    /// Roslyn's conversion classification and the CLR disagree in opposite directions, and the variance /
    /// <c>ValueTuple</c> rows the research named as the anticipated-but-untested third disagreement class.</para>
    /// <para><b>Expected values were generated from live reflection</b> (2026-07-26,
    /// <c>target.IsAssignableFrom(source)</c> over .NET 10) and committed — the corpus asserts "the Roslyn adapter
    /// equals the CLR", never "equals what the author believed". The reflection-side driver re-derives them from
    /// the live relation on every run, so a wrong committed value is a red build on both sides.</para>
    /// <para>Spellings are both legal C# (for the symbol driver's <c>typeof</c> probe) and resolvable by
    /// <c>ReflectionHelper.ResolveType</c> (for the reflection driver).</para>
    /// </summary>
    internal static class AssignabilityCorpus
    {
        internal static readonly AssignabilityRow[] Rows =
        {
            new AssignabilityRow("System.String", "System.String", true, "identity"),
            new AssignabilityRow("System.Object", "System.Object", true, "identity"),
            new AssignabilityRow("System.String", "System.Object", true, "reference"),
            new AssignabilityRow("System.Object", "System.String", false, "reference"),

            new AssignabilityRow("System.Int32", "System.Object", true, "boxing"),
            new AssignabilityRow("System.Int32", "System.IComparable", true, "boxing"),
            new AssignabilityRow("System.Int32", "System.Enum", false, "boxing"),
            new AssignabilityRow("System.DayOfWeek", "System.Enum", true, "boxing"),
            new AssignabilityRow("System.Decimal", "System.Object", true, "boxing"),
            new AssignabilityRow("System.DateTime", "System.ValueType", true, "boxing"),

            // Nullable lift — the two correction rows. Roslyn classifies (1) ImplicitNullable and (2) boxing;
            // the CLR answers true and false respectively, and the adapter must return the CLR answer.
            new AssignabilityRow("System.Int32", "System.Nullable<System.Int32>", true, "nullable-correction-A"),
            new AssignabilityRow("System.Nullable<System.Int32>", "System.IComparable", false, "nullable-correction-C"),
            new AssignabilityRow("System.Nullable<System.Int32>", "System.Int32", false, "nullable"),

            // The same correction with a class target rather than an interface one. Roslyn classifies the boxed
            // enum, which does derive from Enum; the CLR relates Nullable<T> itself, whose base chain is
            // ValueType and object and stops there. The two rows under it are the neighbours the correction must
            // not swallow — they are on that base chain.
            new AssignabilityRow("System.Nullable<System.DayOfWeek>", "System.Enum", false,
                "nullable-correction-C"),
            new AssignabilityRow("System.Nullable<System.DayOfWeek>", "System.ValueType", true, "nullable"),
            new AssignabilityRow("System.Nullable<System.DayOfWeek>", "System.Object", true, "nullable"),

            // Nullable-to-nullable (assignability, distinct from conversion legality)
            new AssignabilityRow("System.Nullable<System.Int32>", "System.Nullable<System.Int64>", false, "nullable"),
            new AssignabilityRow("System.Nullable<System.Int32>", "System.Nullable<System.Int32>", true, "nullable"),

            // Numeric — pins that widening legality never leaks into the assignability answer.
            new AssignabilityRow("System.Int32", "System.Int64", false, "numeric"),

            new AssignabilityRow("System.ArgumentException", "System.Exception", true, "hierarchy"),
            new AssignabilityRow("System.Exception", "System.ArgumentException", false, "hierarchy"),
            new AssignabilityRow("System.String", "System.IComparable", true, "hierarchy"),
            new AssignabilityRow("System.Collections.Generic.IList<System.Int32>",
                "System.Collections.Generic.ICollection<System.Int32>", true, "hierarchy"),
            new AssignabilityRow("System.Collections.Generic.List<System.String>",
                "System.Collections.Generic.IEnumerable<System.String>", true, "hierarchy"),
            new AssignabilityRow("System.String", "System.Collections.Generic.IEnumerable<System.Char>", true,
                "hierarchy"),

            // Variance probes — the anticipated third-disagreement class (value-type type arguments).
            new AssignabilityRow("System.Collections.Generic.IEnumerable<System.String>",
                "System.Collections.Generic.IEnumerable<System.Object>", true, "variance"),
            new AssignabilityRow("System.Collections.Generic.IEnumerable<System.Int32>",
                "System.Collections.Generic.IEnumerable<System.Object>", false, "variance"),

            new AssignabilityRow("(System.Int32, System.String)", "(System.Int32, System.String)", true, "valuetuple"),
            new AssignabilityRow("(System.Int32, System.String)", "(System.Int64, System.String)", false, "valuetuple"),
            new AssignabilityRow("System.ValueTuple<System.Int32, System.String>",
                "(System.Int32, System.String)", true, "valuetuple"),

            new AssignabilityRow("System.String[]", "System.Object[]", true, "array"),
            new AssignabilityRow("System.Int32[]", "System.Object[]", false, "array"),
            new AssignabilityRow("System.Int32[]", "System.Array", true, "array"),
            new AssignabilityRow("System.Int32[]", "System.Collections.Generic.IEnumerable<System.Int32>", true,
                "array"),

            // Array covariance over element types the CLR reduces to one — each signed/unsigned integer pair, and
            // an enum with its underlying primitive. Roslyn classifies none of these as a conversion at all, so a
            // classification-based adapter refuses every one.
            new AssignabilityRow("System.UInt32[]", "System.Int32[]", true, "array-covariance"),
            new AssignabilityRow("System.Int32[]", "System.UInt32[]", true, "array-covariance"),
            new AssignabilityRow("System.Byte[]", "System.SByte[]", true, "array-covariance"),
            new AssignabilityRow("System.Int64[]", "System.UInt64[]", true, "array-covariance"),
            new AssignabilityRow("System.DayOfWeek[]", "System.Int32[]", true, "array-covariance"),
            new AssignabilityRow("System.Int32[][]", "System.UInt32[][]", true, "array-covariance"),
            new AssignabilityRow("System.UInt32[]", "System.Collections.Generic.IList<System.Int32>", true,
                "array-covariance"),
            new AssignabilityRow("System.DayOfWeek[]",
                "System.Collections.Generic.IEnumerable<System.Int32>", true, "array-covariance"),

            // What the same rule must still refuse: a differing width, a value-type element into a reference-type
            // element (the direction that is false at runtime however it is spelled), and the two same-width pairs
            // the CLR does not reduce.
            new AssignabilityRow("System.Int32[]", "System.Int64[]", false, "array-covariance"),
            new AssignabilityRow("System.Int32[]", "System.ValueType[]", false, "array-covariance"),
            new AssignabilityRow("System.DayOfWeek[]", "System.Enum[]", false, "array-covariance"),
            new AssignabilityRow("System.Int32[]", "System.Collections.Generic.IEnumerable<System.Object>", false,
                "array-covariance"),
            new AssignabilityRow("System.Char[]", "System.UInt16[]", false, "array-covariance"),
            new AssignabilityRow("System.Boolean[]", "System.Byte[]", false, "array-covariance"),

            // The 16-bit pair. The other three widths were here from the start; this one was not, and its case in
            // the reduction could be deleted without a single test noticing.
            new AssignabilityRow("System.UInt16[]", "System.Int16[]", true, "array-covariance"),
            new AssignabilityRow("System.Int16[]", "System.UInt16[]", true, "array-covariance"),

            // The pointer-width pair, which the C# numeric-conversion table the reduction otherwise reads from
            // does not name at all.
            new AssignabilityRow("System.IntPtr[]", "System.UIntPtr[]", true, "array-covariance"),
            new AssignabilityRow("System.UIntPtr[]", "System.IntPtr[]", true, "array-covariance"),
            new AssignabilityRow("System.IntPtr[]", "System.Int64[]", false, "array-covariance"),

            // Reducing the arrays alone answers only the direction where the source is what reduces. These are the
            // other one: the array interface's own type argument reduces, against an array and nothing else.
            new AssignabilityRow("System.Int32[]", "System.Collections.Generic.IList<System.UInt32>", true,
                "array-interface-covariance"),
            new AssignabilityRow("System.Int32[]", "System.Collections.Generic.IEnumerable<System.UInt32>", true,
                "array-interface-covariance"),
            new AssignabilityRow("System.Int32[]", "System.Collections.Generic.IReadOnlyList<System.UInt32>", true,
                "array-interface-covariance"),
            new AssignabilityRow("System.Int32[]", "System.Collections.Generic.IList<System.DayOfWeek>", true,
                "array-interface-covariance"),
            new AssignabilityRow("System.DayOfWeek[]", "System.Collections.Generic.IEnumerable<System.UInt32>", true,
                "array-interface-covariance"),
            new AssignabilityRow("System.UInt32[]", "System.Collections.Generic.IList<System.DayOfWeek>", true,
                "array-interface-covariance"),

            // The neighbours that keep it a rule about arrays and about reduction: an interface source is not an
            // array however its argument reduces, a differing width still differs, and a lifted element reduces
            // to nothing.
            new AssignabilityRow("System.Collections.Generic.IList<System.Int32>",
                "System.Collections.Generic.IList<System.UInt32>", false, "array-interface-covariance"),
            new AssignabilityRow("System.Int32[]", "System.Collections.Generic.IList<System.Int64>", false,
                "array-interface-covariance"),
            new AssignabilityRow("System.Nullable<System.UInt32>[]", "System.Nullable<System.Int32>[]", false,
                "array-covariance"),

            // Rank. The reduction rebuilds the array around the reduced element, and rebuilding it at rank 1
            // would make every one of these true.
            new AssignabilityRow("System.Int32[,]", "System.UInt32[,]", true, "array-rank"),
            new AssignabilityRow("System.DayOfWeek[,]", "System.Int32[,]", true, "array-rank"),
            new AssignabilityRow("System.Int32[]", "System.UInt32[,]", false, "array-rank"),
            new AssignabilityRow("System.Int32[,]", "System.UInt32[]", false, "array-rank"),
            new AssignabilityRow("System.DayOfWeek[,]", "System.Int32[]", false, "array-rank"),
            new AssignabilityRow("System.Int32[][]", "System.UInt32[][,]", false, "array-rank"),
            new AssignabilityRow("System.Int32[,]", "System.Collections.Generic.IList<System.UInt32>", false,
                "array-rank"),
        };
    }
}
