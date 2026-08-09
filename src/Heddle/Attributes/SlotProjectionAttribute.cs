using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Declares that an extension is a <b>slot projection</b>: inside a definition body that declares a slot
    /// parameter (<c>&lt;name(out:: Type)&gt;</c>) the call carries the slot's value and renders the caller's
    /// content in its place, and outside one it splices the caller's content against the enclosing model. Both
    /// tiers treat any extension carrying this attribute exactly like the built-in <c>@out</c>, so the projection
    /// is a declared role rather than a name the compiler knows.</para>
    /// <para>Carrying it obliges the extension to read the enclosing definition's slot type off
    /// <c>CompileContext.SlotParameterType</c> in its own <c>InitStart</c> and decide for itself whether it is
    /// projecting a slot value; to report the slot diagnostics that reading implies (a missing value, a value
    /// beside a body, a value the declared slot type cannot take); and to render through the scope's slot carrier
    /// rather than through its own body. The attribute grants none of that — it only says the extension does it.
    /// A bodied call, and a valueless call inside a slot-declaring definition, are not shapes a projection can
    /// carry.</para>
    /// <para>Inherited by derived extensions, so a custom extension deriving a projection keeps the role
    /// automatically. Checked at compile time only — never at render time.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class SlotProjectionAttribute : Attribute
    {
    }
}
