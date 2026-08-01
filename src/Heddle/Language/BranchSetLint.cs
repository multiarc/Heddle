using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Language
{
    /// <summary>
    /// The branch-set diagnostics carried by <see cref="DocumentShaping.StripBranchSets"/>'s event stream —
    /// stripped interleaved text, the orphan state machine, and the ignored-terminal-condition and
    /// missing-scope-channel checks. It lives beside the strip machine rather than in either compiler so the
    /// two tiers raise these from one implementation over one event order, instead of each deciding the same
    /// conditions for itself.
    /// <para>Everything it reads is parse data plus one predicate about the extension type, so the build tier
    /// answers it from Roslyn symbols and the run tier from reflection, and the text, fix and position are the
    /// same object on both.</para>
    /// </summary>
    internal sealed class BranchSetLint : DocumentShaping.IBranchStripObserver
    {
        private enum OrphanState
        {
            None,
            Open,
            Closed,
            Unknown
        }

        private readonly ICollection<HeddleCompileWarning> _warnings;
        private readonly ICollection<HeddleCompileError> _errors;
        private readonly Func<string, bool> _hasScopeChannel;
        private OrphanState _state = OrphanState.None;

        /// <param name="errors">The orphan-terminal channel. A host that reports warnings only passes
        /// <c>null</c>; the state machine still advances, so the warning arms see the same states.</param>
        /// <param name="hasScopeChannel">Whether the named extension type carries <c>[ScopeChannel]</c>. Only
        /// consulted for a chain already classified <see cref="DocumentShaping.BranchKind.Continuation"/> or
        /// <see cref="DocumentShaping.BranchKind.Terminal"/>, which both require the name to have resolved to an
        /// extension type, so a predicate that answers false for an unresolved name cannot widen the check.</param>
        internal BranchSetLint(ICollection<HeddleCompileWarning> warnings, ICollection<HeddleCompileError> errors,
            Func<string, bool> hasScopeChannel)
        {
            _warnings = warnings;
            _errors = errors;
            _hasScopeChannel = hasScopeChannel;
        }

        public void OnClassified(OutputChain chain, OutputItem leftmost, DocumentShaping.BranchKind kind)
        {
            if (kind != DocumentShaping.BranchKind.Continuation && kind != DocumentShaping.BranchKind.Terminal)
                return;
            if (leftmost == null)
                return;
            var name = leftmost.ExtensionName;
            if (string.IsNullOrEmpty(name) || _hasScopeChannel(name))
                return;

            _warnings.Add(new HeddleCompileWarning
            {
                Error =
                    $"A branch continuation/terminal '@{name}' does not carry [ScopeChannel]; it cannot read the branch state at render time.",
                Fix = "Add [ScopeChannel] to the extension so it can read the branch state.",
                Position = leftmost.Position,
                DiagnosticId = HeddleDiagnosticIds.BranchRoleMissingScopeChannel
            });
        }

        public void OnGapCollected(OutputChain prev, OutputChain next, OutputItem nextLeftmost,
            Strings.Core.BlockPosition gap, string gapText)
        {
            if (!string.IsNullOrWhiteSpace(gapText) && nextLeftmost != null)
            {
                _warnings.Add(new HeddleCompileWarning
                {
                    Error = "Text between branch blocks is never rendered.",
                    Fix = "Move it before the '@if', after the last branch, or into a branch body.",
                    Position = nextLeftmost.Position,
                    DiagnosticId = HeddleDiagnosticIds.BranchTextStripped
                });
            }
        }

        public void OnBlockCompleted(OutputChain chain, OutputItem leftmost, DocumentShaping.BranchKind kind)
        {
            switch (kind)
            {
                case DocumentShaping.BranchKind.Opener:
                    _state = OrphanState.Open;
                    break;

                case DocumentShaping.BranchKind.Continuation:
                    if (_state == OrphanState.None || _state == OrphanState.Closed)
                    {
                        _warnings.Add(new HeddleCompileWarning
                        {
                            Error =
                                $"'@{leftmost.ExtensionName}' is a branch continuation with no preceding opener in this scope — it starts a new set.",
                            Fix =
                                "Open the set with a branch opener (such as '@if(...)'), or use a standalone opener if an independent condition is intended.",
                            Position = leftmost.Position,
                            DiagnosticId = HeddleDiagnosticIds.ElifWithoutIf
                        });
                    }

                    _state = OrphanState.Open;
                    break;

                case DocumentShaping.BranchKind.Terminal:
                    if (leftmost != null && !IsEmptyParameter(leftmost))
                    {
                        _warnings.Add(new HeddleCompileWarning
                        {
                            Error = "A branch terminal takes no condition — its parameter is ignored.",
                            Fix = "Use a branch continuation (such as '@elif(...)') for a conditional branch, or remove the parameter.",
                            Position = leftmost.Position,
                            DiagnosticId = HeddleDiagnosticIds.ElseConditionIgnored
                        });
                    }

                    if (_state == OrphanState.None || _state == OrphanState.Closed)
                    {
                        _errors?.Add(
                            $"'@{leftmost?.ExtensionName}' is a branch terminal with no matching opener in this scope."
                                .ToError(leftmost?.Position ?? chain.BlockPosition,
                                    HeddleDiagnosticIds.ElseWithoutIf));
                        // state unchanged — a further orphan @else errors again.
                    }
                    else
                    {
                        _state = OrphanState.Closed;
                    }

                    break;

                case DocumentShaping.BranchKind.Participant:
                    _state = OrphanState.Unknown;
                    break;

                default: // Other
                    // Non-branch blocks leave runtime frame intact so following @else can still bind.
                    break;
            }
        }

        private static bool IsEmptyParameter(OutputItem item)
        {
            var callParameter = item.CallParameter;
            if (!callParameter.IsModelTypeParameter)
                return false; // chain / C# / native expression parameter — non-empty
            return callParameter.ModelParameter == null || callParameter.ModelParameter.Length == 0 ||
                   string.IsNullOrEmpty(callParameter.ModelParameter[0]);
        }
    }
}
