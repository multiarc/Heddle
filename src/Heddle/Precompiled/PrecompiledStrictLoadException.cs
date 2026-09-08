using System;

namespace Heddle.Precompiled
{
    /// <summary>Thrown at materialization under <c>PrecompiledStrictLoad</c> when a site would compile
    /// at load.</summary>
    public sealed class PrecompiledStrictLoadException : InvalidOperationException
    {
        public PrecompiledStrictLoadException(string templateKey, int siteOrdinal, string siteKind)
            : base("Template '" + templateKey + "' would compile a " + siteKind + " site at load " +
                "(ordinal " + siteOrdinal + ") under PrecompiledStrictLoad.")
        {
            TemplateKey = templateKey;
            SiteOrdinal = siteOrdinal;
            SiteKind = siteKind;
        }

        public string TemplateKey { get; }

        public int SiteOrdinal { get; }

        /// <summary>"MemberAccessor", "NativeExpression", "CSharp", "LateBound" or "RefusalSite".</summary>
        public string SiteKind { get; }
    }
}
