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
        /// The call item being compiled (D16). Populated by <c>InitializeTemplate</c>'s call sites; used by
        /// <c>OutExtension.InitStart</c> to see its call parameter at compile time (the slot-projection checks).
        /// Internal — not part of the public extension surface.
        /// </summary>
        internal OutputItem SourceItem;
    }
}