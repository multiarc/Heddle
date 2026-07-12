using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions {
    // Archived tombstone (import-removal-spec D3/D8): '@import' is removed. This type is retained ONLY so the
    // reflection-based extension scan (TemplateFactory.LoadExtensions) still resolves the 'import' name on the
    // dynamic tier — without it, TemplateFactory.Create adds a second, generic "Cannot find extension <import>"
    // diagnostic and the dynamic tier diverges from the generator (cross-tier identity, G2). It is instantiated
    // only via Activator.CreateInstance, which ignores [Obsolete], so the tombstone keeps working at runtime while
    // any *source* reference to it — including deriving from it — is a hard compile error.
    [Obsolete("'@import' has been removed and this extension is an inert tombstone. Do not use or derive from it. " +
              "Use '@<<{{ path }}' to share definitions and layouts, or '@partial(){{ name }}' to embed rendered output.",
        error: true)]
    [ExtensionName("import")]
    public sealed class ImportExtension : AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            // Tombstone (import-removal-spec D3): '@import' is removed. The positioned HED4003 removal error is
            // raised at the shared parse layer (HeddleMainListener.ExitExtension_id). This extension stays
            // registered ONLY so TemplateFactory.Create("import") resolves the name on the dynamic tier; without
            // it, Create adds a second, generic "Cannot find extension <import>" diagnostic, and the dynamic tier
            // would show two diagnostics where the generator shows one (breaking cross-tier identity). It reads no
            // file, merges nothing, stamps no provenance, and produces no output.
            return null;
        }

        public override object ProcessData(in Scope scope)
        {
            return null;
        }

        public override void RenderData(in Scope scope)
        {
        }
    }
}
