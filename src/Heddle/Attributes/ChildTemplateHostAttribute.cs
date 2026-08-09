using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Declares that an extension is a <b>child-template host</b>: its body is not content but a name, which
    /// it resolves at compile time to a second template, compiles as a child of the one being compiled, and hosts
    /// — rendering that child's output in place of its own body. Both tiers treat any extension carrying this
    /// attribute exactly like the built-in <c>@partial</c>, so hosting is a declared role rather than a name the
    /// compiler knows.</para>
    /// <para>Carrying it obliges the extension to evaluate its own body once, at compile time, to produce the child
    /// template's name; to queue that compile through <c>CompileContext.AddDelayedCompileTemplate</c> so the child
    /// is compiled with the call value's type as its model and its errors are collected into the parent's compile
    /// result; and to take delivery of the compiled child in <c>CompleteInit</c> and render it. The attribute
    /// grants none of that — it only says the extension does it. A body that names nothing is not a shape a host
    /// can carry.</para>
    /// <para>Inherited by derived extensions, so a custom extension deriving a host keeps the role automatically.
    /// Checked at compile time only — never at render time.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ChildTemplateHostAttribute : Attribute
    {
    }
}
