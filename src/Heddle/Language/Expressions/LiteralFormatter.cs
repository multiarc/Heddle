using System.Globalization;

namespace Heddle.Language.Expressions
{
    /// <summary>
    /// Inverse of <see cref="ExpressionAstBuilder"/>: round-trips decoded literals to invariant C# literals preserving CLR type and value.
    /// Correctness requirement: <c>decode(format(v)) == v</c> bit-for-bit. Reals use <c>G9</c>/<c>G17</c> (not <c>"R"</c>)
    /// because <c>"R"</c> does not round-trip <c>double</c> on .NET Framework.
    /// </summary>
    internal static class LiteralFormatter
    {
        /// <summary>Formats the boxed literal as a C# literal, or null if no literal form exists (caller degrades).</summary>
        public static string Format(object value)
        {
            switch (value)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case string s: return CSharpEscape.StringLiteral(s);
                case char c: return CSharpEscape.CharLiteral(c);
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case uint ui: return ui.ToString(CultureInfo.InvariantCulture) + "U";
                case long l: return l.ToString(CultureInfo.InvariantCulture) + "L";
                case ulong ul: return ul.ToString(CultureInfo.InvariantCulture) + "UL";
                // C# has no literal for a non-finite real: `G9`/`G17` spell these `Infinity`, `-Infinity` and `NaN`,
                // which with the suffix appended are identifiers the consumer's compiler does not know. There is no
                // literal form, so the caller degrades — which is what this method's null already means.
                case float f: return float.IsNaN(f) || float.IsInfinity(f)
                    ? null
                    : f.ToString("G9", CultureInfo.InvariantCulture) + "F";
                case double d: return double.IsNaN(d) || double.IsInfinity(d)
                    ? null
                    : d.ToString("G17", CultureInfo.InvariantCulture) + "D";
                case decimal m: return m.ToString(CultureInfo.InvariantCulture) + "M";
                default: return null;
            }
        }
    }
}
