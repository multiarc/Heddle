using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Heddle.Exceptions;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// The frozen default whitelist of native-expression functions. Every method is invariant-culture and
    /// exception-safe at render (defensive bodies, not compiler-inserted try/catch). Bound by explicit
    /// <see cref="MethodInfo"/> — never to BCL span overloads — keeping the set interpreter-portable.
    /// </summary>
    internal static class BuiltInFunctions
    {
        internal static string Upper(string value) => value?.ToUpperInvariant() ?? string.Empty;

        internal static string Lower(string value) => value?.ToLowerInvariant() ?? string.Empty;

        internal static string Trim(string value) => value?.Trim() ?? string.Empty;

        internal static int Len(string value) => value?.Length ?? 0;

        internal static bool Contains(string source, string value)
        {
            source ??= string.Empty;
            value ??= string.Empty;
            return source.IndexOf(value, StringComparison.Ordinal) >= 0;
        }

        internal static bool StartsWith(string source, string value)
        {
            source ??= string.Empty;
            value ??= string.Empty;
            return source.StartsWith(value, StringComparison.Ordinal);
        }

        internal static bool EndsWith(string source, string value)
        {
            source ??= string.Empty;
            value ??= string.Empty;
            return source.EndsWith(value, StringComparison.Ordinal);
        }

        internal static string Replace(string source, string oldValue, string newValue)
        {
            if (source == null)
                return string.Empty;
            if (string.IsNullOrEmpty(oldValue))
                return source;
            return source.Replace(oldValue, newValue ?? string.Empty);
        }

        internal static string Substr(string source, int start)
        {
            if (source == null)
                return string.Empty;
            if (start < 0)
                start = 0;
            if (start > source.Length)
                start = source.Length;
            return source.Substring(start);
        }

        internal static string Substr(string source, int start, int length)
        {
            if (source == null)
                return string.Empty;
            if (start < 0)
                start = 0;
            if (start > source.Length)
                start = source.Length;
            if (length < 0)
                length = 0;
            int max = source.Length - start;
            if (length > max)
                length = max;
            return source.Substring(start, length);
        }

        internal static string Format(object value, string format)
        {
            if (value == null)
                return string.Empty;
            if (value is IFormattable formattable)
            {
                try
                {
                    return formattable.ToString(format, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    return string.Empty;
                }
            }

            return Str(value);
        }

        internal static string Format(string format, params object[] args)
        {
            if (format == null)
                return string.Empty;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args ?? new object[0]);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }

        internal static string Str(object value) =>
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        internal static int Abs(int value) => value == int.MinValue ? value : Math.Abs(value);
        internal static long Abs(long value) => value == long.MinValue ? value : Math.Abs(value);
        internal static double Abs(double value) => Math.Abs(value);
        internal static decimal Abs(decimal value) => Math.Abs(value);

        internal static int Min(int a, int b) => Math.Min(a, b);
        internal static long Min(long a, long b) => Math.Min(a, b);
        internal static double Min(double a, double b) => Math.Min(a, b);
        internal static decimal Min(decimal a, decimal b) => Math.Min(a, b);

        internal static int Max(int a, int b) => Math.Max(a, b);
        internal static long Max(long a, long b) => Math.Max(a, b);
        internal static double Max(double a, double b) => Math.Max(a, b);
        internal static decimal Max(decimal a, decimal b) => Math.Max(a, b);

        internal static double Round(double value) => Math.Round(value);
        internal static double Round(double value, int digits) => Math.Round(value, Clamp(digits, 0, 15));
        internal static decimal Round(decimal value) => Math.Round(value);
        internal static decimal Round(decimal value, int digits) => Math.Round(value, Clamp(digits, 0, 28));

        internal static double Floor(double value) => Math.Floor(value);
        internal static decimal Floor(decimal value) => Math.Floor(value);

        internal static double Ceil(double value) => Math.Ceiling(value);
        internal static decimal Ceil(decimal value) => Math.Ceiling(value);

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        /// <summary>
        /// The HED4001 message format (a single <c>{0}</c> for the offending step). Shared verbatim by the
        /// static <see cref="NativeExpressionCompiler"/> literal check and the render-time guard below so the
        /// two enforcement layers can never diverge (phase 4 D3).
        /// </summary>
        internal const string RangeStepMessageFormat =
            "Function 'range' requires a positive step, but {0} was supplied — a zero or negative step never terminates the loop.";

        /// <summary>Two-argument <c>range(start, last)</c> — step 1, <paramref name="last"/>-exclusive.</summary>
        internal static Heddle.Models.Range Range(int start, int last) => new Heddle.Models.Range(start, last);

        /// <summary>
        /// Three-argument <c>range(start, last, step)</c>. A non-positive <paramref name="step"/> would make
        /// <c>ForIndexExtension</c>'s <c>Last</c>-exclusive loop never terminate (or silently render nothing),
        /// so it throws at render — the sole sanctioned built-in throw (phase 4 D3). A statically-visible
        /// literal step is caught earlier as HED4001; this covers the model-driven case.
        /// </summary>
        internal static Heddle.Models.Range Range(int start, int last, int step)
        {
            if (step <= 0)
                throw new TemplateProcessingException(string.Format(CultureInfo.InvariantCulture,
                    RangeStepMessageFormat, step));
            return new Heddle.Models.Range(start, last, step);
        }

        /// <summary>Builds the registry entries binding each default name to its explicit <see cref="MethodInfo"/>.</summary>
        internal static IEnumerable<FunctionEntry> CreateEntries()
        {
            yield return Bind("upper", nameof(Upper), typeof(string));
            yield return Bind("lower", nameof(Lower), typeof(string));
            yield return Bind("trim", nameof(Trim), typeof(string));
            yield return Bind("len", nameof(Len), typeof(string));
            yield return Bind("contains", nameof(Contains), typeof(string), typeof(string));
            yield return Bind("startswith", nameof(StartsWith), typeof(string), typeof(string));
            yield return Bind("endswith", nameof(EndsWith), typeof(string), typeof(string));
            yield return Bind("replace", nameof(Replace), typeof(string), typeof(string), typeof(string));
            yield return Bind("substr", nameof(Substr), typeof(string), typeof(int));
            yield return Bind("substr", nameof(Substr), typeof(string), typeof(int), typeof(int));
            yield return Bind("format", nameof(Format), typeof(object), typeof(string));
            yield return Bind("format", nameof(Format), typeof(string), typeof(object[]));
            yield return Bind("str", nameof(Str), typeof(object));
            yield return Bind("abs", nameof(Abs), typeof(int));
            yield return Bind("abs", nameof(Abs), typeof(long));
            yield return Bind("abs", nameof(Abs), typeof(double));
            yield return Bind("abs", nameof(Abs), typeof(decimal));
            yield return Bind("min", nameof(Min), typeof(int), typeof(int));
            yield return Bind("min", nameof(Min), typeof(long), typeof(long));
            yield return Bind("min", nameof(Min), typeof(double), typeof(double));
            yield return Bind("min", nameof(Min), typeof(decimal), typeof(decimal));
            yield return Bind("max", nameof(Max), typeof(int), typeof(int));
            yield return Bind("max", nameof(Max), typeof(long), typeof(long));
            yield return Bind("max", nameof(Max), typeof(double), typeof(double));
            yield return Bind("max", nameof(Max), typeof(decimal), typeof(decimal));
            yield return Bind("round", nameof(Round), typeof(double));
            yield return Bind("round", nameof(Round), typeof(double), typeof(int));
            yield return Bind("round", nameof(Round), typeof(decimal));
            yield return Bind("round", nameof(Round), typeof(decimal), typeof(int));
            yield return Bind("floor", nameof(Floor), typeof(double));
            yield return Bind("floor", nameof(Floor), typeof(decimal));
            yield return Bind("ceil", nameof(Ceil), typeof(double));
            yield return Bind("ceil", nameof(Ceil), typeof(decimal));
            yield return Bind("range", nameof(Range), typeof(int), typeof(int));
            yield return Bind("range", nameof(Range), typeof(int), typeof(int), typeof(int));
        }

        private static FunctionEntry Bind(string registeredName, string methodName, params Type[] parameterTypes)
        {
            var method = typeof(BuiltInFunctions).GetMethod(methodName,
                BindingFlags.Static | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null)
                throw new MissingMethodException(nameof(BuiltInFunctions), methodName);
            return FunctionEntry.FromMethod(registeredName, method);
        }
    }
}
