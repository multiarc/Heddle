using System.Collections.Generic;

namespace Heddle.Generator.Typing
{
    /// <summary>
    /// The collector behind a <b>type-agnostic</b> body — one the build emits with no model cast because the
    /// extension hosting it decides its model type in a compile-time hook the build has not read.
    /// <para>Everything model-dependent inside such a body becomes an entry here rather than typed C#: a member
    /// read becomes a <c>PrecompiledLateAccessor</c> field, and a nested call site becomes a dependent
    /// <c>PrecompiledInitSite</c>. Both are named on the enclosing site, and <c>PrecompiledRuntime.Init</c> fills
    /// them the moment the hook answers — which is why a nested call's typing cascades from the outer body rather
    /// than standing beside it as a flat sibling.</para>
    /// <para>Carried on <see cref="BodyContext"/>, so "is this body typed by the build or by the hook" is one
    /// nullable reference rather than a flag every rule has to remember to consult.</para>
    /// </summary>
    internal sealed class LateBodySink
    {
        /// <summary>The emitted <c>PrecompiledLateAccessor</c> field names, in emission order.</summary>
        public List<string> Accessors { get; } = new List<string>();

        /// <summary>The emitted <c>PrecompiledInitSite</c> field names of the call sites nested in this body.</summary>
        public List<string> Dependents { get; } = new List<string>();
    }
}
