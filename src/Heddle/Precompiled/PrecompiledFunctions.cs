using Heddle.Runtime.Expressions;

namespace Heddle.Precompiled
{
    /// <summary>
    /// Public façade for the 18 default built-in functions (35 overloads) in generated code: <c>BuiltInFunctions</c>
    /// is internal and consumer assemblies have no IVT. Each method delegates to the internal equivalent; semantics
    /// (invariant culture, uniform null rules, never-throw) exist once. <c>[ExportFunctions]</c> exports bind
    /// directly to their public containers. Thread-safe; no state.
    /// </summary>
    public static class PrecompiledFunctions
    {
        public static string Upper(string value) => BuiltInFunctions.Upper(value);
        public static string Lower(string value) => BuiltInFunctions.Lower(value);
        public static string Trim(string value) => BuiltInFunctions.Trim(value);
        public static int Len(string value) => BuiltInFunctions.Len(value);
        public static bool Contains(string source, string value) => BuiltInFunctions.Contains(source, value);
        public static bool StartsWith(string source, string value) => BuiltInFunctions.StartsWith(source, value);
        public static bool EndsWith(string source, string value) => BuiltInFunctions.EndsWith(source, value);

        public static string Replace(string source, string oldValue, string newValue) =>
            BuiltInFunctions.Replace(source, oldValue, newValue);

        public static string Substr(string source, int start) => BuiltInFunctions.Substr(source, start);
        public static string Substr(string source, int start, int length) =>
            BuiltInFunctions.Substr(source, start, length);

        public static string Format(object value, string format) => BuiltInFunctions.Format(value, format);
        public static string Format(string format, params object[] args) => BuiltInFunctions.Format(format, args);
        public static string Str(object value) => BuiltInFunctions.Str(value);

        public static int Abs(int value) => BuiltInFunctions.Abs(value);
        public static long Abs(long value) => BuiltInFunctions.Abs(value);
        public static double Abs(double value) => BuiltInFunctions.Abs(value);
        public static decimal Abs(decimal value) => BuiltInFunctions.Abs(value);

        public static int Min(int a, int b) => BuiltInFunctions.Min(a, b);
        public static long Min(long a, long b) => BuiltInFunctions.Min(a, b);
        public static double Min(double a, double b) => BuiltInFunctions.Min(a, b);
        public static decimal Min(decimal a, decimal b) => BuiltInFunctions.Min(a, b);

        public static int Max(int a, int b) => BuiltInFunctions.Max(a, b);
        public static long Max(long a, long b) => BuiltInFunctions.Max(a, b);
        public static double Max(double a, double b) => BuiltInFunctions.Max(a, b);
        public static decimal Max(decimal a, decimal b) => BuiltInFunctions.Max(a, b);

        public static double Round(double value) => BuiltInFunctions.Round(value);
        public static double Round(double value, int digits) => BuiltInFunctions.Round(value, digits);
        public static decimal Round(decimal value) => BuiltInFunctions.Round(value);
        public static decimal Round(decimal value, int digits) => BuiltInFunctions.Round(value, digits);

        public static double Floor(double value) => BuiltInFunctions.Floor(value);
        public static decimal Floor(decimal value) => BuiltInFunctions.Floor(value);

        public static double Ceil(double value) => BuiltInFunctions.Ceil(value);
        public static decimal Ceil(decimal value) => BuiltInFunctions.Ceil(value);

        public static Heddle.Models.Range Range(int start, int last) => BuiltInFunctions.Range(start, last);
        public static Heddle.Models.Range Range(int start, int last, int step) => BuiltInFunctions.Range(start, last, step);
    }
}
