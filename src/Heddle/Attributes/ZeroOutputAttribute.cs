using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// Marks an extension as a directive: emits nothing, block removed from output. Protocol: <c>InitStart</c>
    /// returns <c>null</c>, which drops the block. This attribute is the declarative form for the build
    /// (which cannot run <c>InitStart</c> to read return types directly). Without it, custom zero-output extensions
    /// diverged silently between dynamic tier (block removed) and precompiled tier (output kept). Inherited by
    /// derived extensions like <see cref="ScopeChannelAttribute"/>. Declaring it on an extension whose
    /// <c>InitStart</c> does not return <c>null</c> is an error.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ZeroOutputAttribute : Attribute
    {
    }
}
