using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Declares that an extension publishes to or reads from the <see cref="Heddle.Data.Scope"/> local
    /// context channel. Bodies whose compiled document contains such an extension are provisioned with a
    /// locals frame at compile time; without one, <see cref="Heddle.Data.Scope.Publish"/> throws and
    /// <see cref="Heddle.Data.Scope.TryRead"/> returns <c>false</c>.</para>
    /// <para>Inherited by derived extensions, so a custom extension deriving a channel participant keeps
    /// participation automatically. Checked at compile time only — never at render time.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ScopeChannelAttribute : Attribute
    {
    }
}
