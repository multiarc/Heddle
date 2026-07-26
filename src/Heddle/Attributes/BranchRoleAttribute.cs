using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Declares an extension's position in a branch set. The compile-time branch-set scans
    /// (adjacency stripping, orphan diagnostics, locals provisioning of hosted bodies) treat any
    /// extension carrying this attribute exactly like the built-in <c>@if</c>/<c>@elif</c>/<c>@else</c>
    /// family. The attribute adds no render-time behavior — the extension body itself drives the
    /// set through the <see cref="Heddle.Data.Scope"/> channel.</para>
    /// <para>Inherited by derived extensions; checked at compile time only. Continuation and Terminal
    /// extensions read the channel and must also carry <see cref="ScopeChannelAttribute"/>; an Opener
    /// publishes opportunistically and must not carry it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class BranchRoleAttribute : Attribute
    {
        public BranchRoleAttribute(BranchRole role) => Role = role;
        public BranchRole Role { get; }
    }
}
