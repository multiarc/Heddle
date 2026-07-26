using System.Collections.Generic;

namespace Heddle.Precompiled
{
    /// <summary>Implemented by the generated manifest class. Instantiated once per
    /// <see cref="PrecompiledTemplates.Register"/> call; returns one <see cref="PrecompiledTemplateInfo"/> per
    /// precompiled (or fallback-marker) template, ordered ordinally by key.</summary>
    public interface IHeddleTemplateManifest
    {
        IReadOnlyList<PrecompiledTemplateInfo> GetTemplates();
    }
}
