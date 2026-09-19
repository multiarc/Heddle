using System;

namespace Heddle.Precompiled
{
    /// <summary>One refusal site: rebuilt at load from its own source text; a declared exception under strict mode.</summary>
    public readonly struct PrecompiledRefusalSite
    {
        public PrecompiledRefusalSite(
            int siteOrdinal,
            PrecompiledRefusalClass @class,
            string detail,
            int positionStart,
            int positionLength)
        {
            if (detail == null)
                throw new ArgumentNullException(nameof(detail));
            SiteOrdinal = siteOrdinal;
            Class = @class;
            Detail = detail;
            PositionStart = positionStart;
            PositionLength = positionLength;
        }

        public int SiteOrdinal { get; }

        public PrecompiledRefusalClass Class { get; }

        /// <summary>The build's sentence: the extension author's opt-out reason, the enumeration-order
        /// fact, or the unbindable function name.</summary>
        public string Detail { get; }

        public int PositionStart { get; }

        public int PositionLength { get; }
    }
}
