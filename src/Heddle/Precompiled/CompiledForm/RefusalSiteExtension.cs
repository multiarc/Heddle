using System;
using Heddle.Precompiled;

namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>Rebuilds a declared refusal site from its decoded source. Template-level sites already
    /// decode as <see cref="PrecompiledRefusalSite"/>; this covers the parameter-level refusal sources
    /// the loader meets while walking items, so every site the artifact carries becomes the same struct
    /// the registry publishes and strict mode throws.</summary>
    internal static class RefusalSiteExtension
    {
        internal static PrecompiledRefusalSite ToRefusalSite(this CompiledRefusalSource refusal,
            int siteOrdinal)
        {
            if (refusal == null)
                throw new ArgumentNullException(nameof(refusal));
            int start = refusal.Position != null ? refusal.Position.Start : 0;
            int length = refusal.Position != null ? refusal.Position.Length : 0;
            return new PrecompiledRefusalSite(siteOrdinal, refusal.Class, Detail(refusal), start, length);
        }

        private static string Detail(CompiledRefusalSource refusal)
        {
            switch (refusal.Class)
            {
                case PrecompiledRefusalClass.UnbindableCallTyping:
                    return "unbindable call '" + (refusal.SourceText ?? string.Empty) +
                        "': the build registry cannot bind its typing";
                case PrecompiledRefusalClass.ReflectionOrderValue:
                    return "a value the engine types by reflection enumeration order";
                default:
                    return "the extension declares itself not precompilable";
            }
        }
    }
}
