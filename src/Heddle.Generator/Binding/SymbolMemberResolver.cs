using System.Collections.Generic;
using Heddle.Strings.Core;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// Produces HED7008 only for genuine member-path failures that would fail at runtime. Safe dynamic accesses are
    /// never reported as errors; the symbol resolver is at least as permissive as the runtime's reflection walk.
    /// </summary>
    internal static class SymbolMemberResolver
    {
        internal readonly struct MemberFailure
        {
            public MemberFailure(string receiverType, string member, string path, BlockPosition position,
                bool inaccessible = false)
            {
                ReceiverType = receiverType;
                Member = member;
                Path = path;
                Position = position;
                Inaccessible = inaccessible;
            }

            /// <summary>The fully-qualified type of the receiver at the failing segment (HED7008 arg 0).</summary>
            public string ReceiverType { get; }

            /// <summary>The failing segment name (HED7008 arg 1).</summary>
            public string Member { get; }

            /// <summary>The whole dotted member path (HED7008 arg 2).</summary>
            public string Path { get; }

            /// <summary>The <c>.heddle</c> span (absolute template coordinates).</summary>
            public BlockPosition Position { get; }

            /// <summary>The member is there and the engine reads it; only this compilation cannot see it. Reported as
            /// the HED7030 degrade rather than the HED7008 error — the template renders, so the build must not fail.</summary>
            public bool Inaccessible { get; }
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
