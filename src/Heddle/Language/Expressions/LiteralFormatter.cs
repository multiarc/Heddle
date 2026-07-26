using System.Globalization;

namespace Heddle.Language.Expressions
{
    /// <summary>
    /// Round-trips a decoded literal value to an invariant C# literal preserving its exact CLR type and value — the
    /// documented <b>inverse</b> of the AST decoder (<see cref="ExpressionAstBuilder"/>: integer first-fit typing,
    /// suffix-driven real typing, the C# escape set). Both halves compile into the same assembly so the round-trip
    /// property is a shared test rather than a comment — a decoder change now breaks a test instead of only breaking
    /// generated code.
    /// <para>The runtime never re-formats a literal — the native-expression compiler keeps the decoder's boxed value
    /// — so the formatter's only correctness requirement is <c>decode(format(v)) == v</c> bit-for-bit. Reals use
    /// <c>G9</c>/<c>G17</c> (not <c>"R"</c>) because <c>"R"</c> is documented as non-round-tripping for <c>double</c>
    /// under .NET Framework, which is where the generator runs when the build host is Visual Studio or the desktop
    /// <c>VBCSCompiler</c>. Ugly-but-exact text (<c>0.10000000000000001</c>) is the correct choice for generated
    /// source.</para>
    /// </summary>
    internal static class LiteralFormatter
    {
        /// <summary>Formats the boxed literal, or returns null for a value with no C# literal form (the caller then
        /// degrades rather than guessing).</summary>
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
                case float f: return f.ToString("G9", CultureInfo.InvariantCulture) + "F";
                case double d: return d.ToString("G17", CultureInfo.InvariantCulture) + "D";
                case decimal m: return m.ToString(CultureInfo.InvariantCulture) + "M";
                default: return null;
            }
        }
    }
}
