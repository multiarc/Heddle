using Heddle.Language;
using Heddle.Runtime;

namespace Heddle.Core
{
    public struct InitContext
    {
        public InitContext(string parameterTemplate, CompileScope compileScope, ParseContext parseContext)
        {
            ParameterTemplate = parameterTemplate;
            CompileScope = compileScope;
            ParseContext = parseContext;
        }

        public string ParameterTemplate;

        public CompileScope CompileScope;

        public ParseContext ParseContext;

        /// <summary>
        /// The call item being compiled. Populated by <c>InitializeTemplate</c>'s call sites; used by
        /// <c>OutExtension.InitStart</c> to see its call parameter at compile time (the slot-projection checks).
        /// Internal — not part of the public extension surface.
        /// </summary>
        internal OutputItem SourceItem;

        /// <summary>
        /// <para>Whether the call site passed this extension a value — <c>@name(expr)</c> in any of its five
        /// shapes (a member path, a native expression, a C# expression, a chain, named prop arguments) as against
        /// the bare <c>@name()</c>. A <c>[SlotProjection]</c> extension needs it because the same call means
        /// different things with and without one: inside a slot-declaring body a value is the slot value and its
        /// absence is an error, and outside one a value has no slot to go into.</para>
        /// <para><c>false</c> for a call the engine did not record an item for.</para>
        /// </summary>
        public bool CallCarriesValue => SourceItem != null && SlotRules.HasOutValue(SourceItem.CallParameter);

        /// <summary>
        /// <para>Whether this call has a producer to its right in its own chain (<c>@wrap():name()</c>), so its
        /// content arrives on the chained channel rather than as a caller body. A <c>[SlotProjection]</c> extension
        /// reads it to refuse composition: there is no caller content to project when the content came from a
        /// chained producer, and projecting the enclosing definition's would render the wrong body.</para>
        /// </summary>
        public bool IsChainedConsumer => SourceItem != null && SourceItem.IsChainedConsumer;
    }
}