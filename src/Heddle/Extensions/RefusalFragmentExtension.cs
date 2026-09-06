using Heddle.Core;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle.Extensions
{
    /// <summary>A refusal site at load: compiles its recorded source text as its own document and renders
    /// through that document's strategy. The loader builds it directly (never through
    /// <c>TemplateFactory</c>, which cannot see it — it carries no <c>[ExtensionName]</c>), so refused
    /// call sites render by recompiling their fragment under the ambient request options while the rest
    /// of the row stays precompiled. Fragment compile errors are re-anchored onto the call's position by
    /// the loader before this instance is built.</summary>
    internal sealed class RefusalFragmentExtension : AbstractExtension
    {
        private readonly RuntimeDocument _document;

        internal RefusalFragmentExtension(RuntimeDocument document, BlockPosition callPosition)
        {
            _document = document ?? throw new System.ArgumentNullException(nameof(document));
            Position = callPosition;
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType,
            ExType parent)
        {
            return dataType;
        }

        public override object ProcessData(in Scope scope) => _document.Strategy.Execute(scope);

        public override void RenderData(in Scope scope) => _document.Strategy.Render(scope);
    }
}
