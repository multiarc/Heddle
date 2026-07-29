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
            public MemberFailure(ITypeSymbol receiver, string member, string path, BlockPosition position,
                bool inaccessible = false)
            {
                Receiver = receiver;
                Member = member;
                Path = path;
                Position = position;
                Inaccessible = inaccessible;
            }

            /// <summary>The receiver at the failing segment. Carried as a symbol rather than a name because telling
            /// "not there" from "not shown to this compilation" means looking at its members again, and that question
            /// is deferred to whoever actually writes a diagnostic.</summary>
            public ITypeSymbol Receiver { get; }

            /// <summary>The fully-qualified type of the receiver at the failing segment (HED7008 arg 0).</summary>
            public string ReceiverType => SymbolTypeResolver.FullyQualified(Receiver);

            /// <summary>The failing segment name (HED7008 arg 1).</summary>
            public string Member { get; }

            /// <summary>The whole dotted member path (HED7008 arg 2).</summary>
            public string Path { get; }

            /// <summary>The <c>.heddle</c> span (absolute template coordinates).</summary>
            public BlockPosition Position { get; }

            /// <summary>The member resolved and this compilation may not name it — the half of the question the walk
            /// answers for free. The other half, a member the symbol model was never shown at all, costs a probe and
            /// is asked at report time.</summary>
            public bool Inaccessible { get; }
        }
    }
}
