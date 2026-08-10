using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions {
    // Tombstone to prevent cross-tier divergence: the dynamic tier must resolve 'import'
    // even though it's removed, or TemplateFactory.Create would generate a diagnostic that
    // the generator does not produce. [Obsolete] blocks source references while allowing
    // runtime instantiation via reflection.
    [Obsolete("'@import' has been removed and this extension is an inert tombstone. Do not use or derive from it. " +
              "Use '@<<{{ path }}' to share definitions and layouts, or '@partial(){{ name }}' to embed rendered output.",
        error: true)]
    [ExtensionName("import")]
    [ZeroOutput]
    public sealed class ImportExtension : AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            // The removal error (HED4003) is raised at the parse layer, so this method returns null.
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
