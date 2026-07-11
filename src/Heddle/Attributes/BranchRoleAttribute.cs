using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Position an extension occupies inside a branch set (compile-time set structuring).</para>
    /// <para>The numeric values are a pinned cross-assembly contract: the source generator cannot reference this
    /// assembly's types and decodes the attribute's constructor argument from Roslyn metadata as a raw <c>int</c>,
    /// so renumbering any member (or reordering, which renumbers implicitly) is a breaking change.</para>
    /// </summary>
    public enum BranchRole
    {
        /// <summary>First: opens a set and publishes the initial <see cref="Heddle.Data.BranchState"/>;
        /// needs no predecessor. Built-ins: <c>@if</c>, <c>@ifnot</c>.</summary>
        Opener = 0,

        /// <summary>Middle: requires a preceding opener or continuation in the same set; may republish
        /// (change) the <see cref="Heddle.Data.BranchState"/>; never first, never last; keeps the
        /// terminal optional. Built-in: <c>@elif</c>/<c>@elseif</c>.</summary>
        Continuation = 1,

        /// <summary>Last: closes a set and clears its state; optional — a set may validly end without
        /// one; takes no condition. Built-in: <c>@else</c>.</summary>
        Terminal = 2
    }

    /// <summary>
    /// <para>Declares an extension's position in a branch set. The compile-time branch-set scans
    /// (adjacency stripping, orphan diagnostics, locals provisioning of hosted bodies) treat any
    /// extension carrying this attribute exactly like the built-in <c>@if</c>/<c>@elif</c>/<c>@else</c>
    /// family. The attribute adds no render-time behavior — the extension body itself drives the
    /// set through the <see cref="Heddle.Data.Scope"/> channel.</para>
    /// <para>Inherited by derived extensions; checked at compile time only. Continuation and Terminal
    /// extensions read the channel and must also carry <see cref="ScopeChannelAttribute"/>; an Opener
    /// publishes opportunistically and must not (see docs/custom-extensions.md).</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class BranchRoleAttribute : Attribute
    {
        public BranchRoleAttribute(BranchRole role) => Role = role;
        public BranchRole Role { get; }
    }
}
