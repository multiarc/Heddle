using System.Collections.Generic;
using Heddle.Strings.Core;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// Milestone 2 (D3 / WI11): describes a <b>genuine</b> member-path failure for <c>HED7008</c>. It reuses
    /// <see cref="SymbolTypeResolver.ResolvePath"/> — the same tier-order property walk milestone 1 uses to type
    /// hops — and produces a diagnostic only when the walk fails with <see cref="SymbolTypeResolver.PathKind.Failed"/>
    /// (a property that does not exist on a resolved, non-dynamic receiver — exactly the condition the runtime member
    /// tier raises as <c>HED0001</c>). A <see cref="SymbolTypeResolver.PathKind.DynamicHop"/> or a resolved path never
    /// produces a failure, so a safe dynamic member access is never turned into a build error. Because the symbol
    /// resolver is at least as permissive as the runtime's reflection walk (it also sees inherited-interface and
    /// explicitly-implemented members), a symbol <c>Failed</c> implies the runtime fails too — the reconciliation the
    /// milestone requires (no safe fallback becomes a hard error unless it is a genuine unresolvable symbol).
    /// </summary>
    internal static class SymbolMemberResolver
    {
        internal readonly struct MemberFailure
        {
            public MemberFailure(string receiverType, string member, string path, BlockPosition position)
            {
                ReceiverType = receiverType;
                Member = member;
                Path = path;
                Position = position;
            }

            /// <summary>The fully-qualified type of the receiver at the failing segment (HED7008 arg 0).</summary>
            public string ReceiverType { get; }

            /// <summary>The failing segment name (HED7008 arg 1).</summary>
            public string Member { get; }

            /// <summary>The whole dotted member path (HED7008 arg 2).</summary>
            public string Path { get; }

            /// <summary>The <c>.heddle</c> span (absolute template coordinates).</summary>
            public BlockPosition Position { get; }
        }

        /// <summary>Resolves <paramref name="segments"/> off <paramref name="start"/>; returns a
        /// <see cref="MemberFailure"/> only when the walk genuinely fails (property not found), otherwise null.</summary>
        public static MemberFailure? TryDescribeFailure(SymbolTypeResolver resolver, ITypeSymbol start,
            IReadOnlyList<string> segments, BlockPosition position)
        {
            if (resolver == null || start == null || segments == null || segments.Count == 0)
                return null;

            var res = resolver.ResolvePath(start, segments);
            if (res.Kind != SymbolTypeResolver.PathKind.Failed)
                return null;

            var idx = res.DynamicIndex;
            var receiver = res.Hops.Count == 0 ? start : res.Hops[res.Hops.Count - 1].Property;
            var member = idx >= 0 && idx < segments.Count ? segments[idx] : segments[segments.Count - 1];
            return new MemberFailure(SymbolTypeResolver.FullyQualified(receiver), member,
                string.Join(".", segments), position);
        }
    }
}
