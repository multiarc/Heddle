using System;

namespace Heddle.Performance.ThirdParty
{
    /// <summary>
    /// Host functions the Heddle entry registers on its <c>FunctionRegistry</c> (the whole trust
    /// boundary for a Native-mode template). <see cref="Truncate"/> mirrors the Liquid
    /// <c>truncate</c> filter that <c>product.liquid</c> uses (and the Handlebars <c>truncate</c>
    /// helper) exactly: when the string is longer than <paramref name="length"/>, keep the first
    /// <c>length - "...".Length</c> characters and append the <c>"..."</c> ellipsis; otherwise pass
    /// the value through. Invariant, allocation-light, exception-free — safe to expose to templates.
    /// </summary>
    public static class HeddleFilters
    {
        private const string Ellipsis = "...";

        public static string Truncate(string value, int length)
        {
            value ??= string.Empty;
            if (value.Length <= length)
                return value;

            var keep = Math.Max(0, length - Ellipsis.Length);
            return string.Concat(value.AsSpan(0, keep), Ellipsis);
        }
    }
}
