using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Declares that an extension is a <b>directive</b>: it emits nothing, and its whole block is removed
    /// from the document rather than kept as rendered output. The four built-in directives —
    /// <c>@model</c>, <c>@using</c>, <c>@import</c> and <c>@profile</c> — carry it.</para>
    /// <para>The runtime's protocol for this is behavioral: a directive's <c>InitStart</c> returns <c>null</c>,
    /// which is what makes the compiler drop the block. That protocol stays authoritative — this attribute is the
    /// <em>declarative</em> form of it, and it exists because a build-time generator can only read symbols. Before
    /// it, a custom zero-output extension diverged silently: the dynamic tier removed its block and the
    /// precompiled tier kept it as rendered output.</para>
    /// <para>Inherited by derived extensions, like <see cref="ScopeChannelAttribute"/>. Declaring it on an
    /// extension whose <c>InitStart</c> does <em>not</em> return <c>null</c> is an authoring error; a conformance
    /// test asserts the two agree for every built-in.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ZeroOutputAttribute : Attribute
    {
    }
}
