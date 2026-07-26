using System;
using System.Collections.Generic;

namespace Heddle.Language
{
    /// <summary>
    /// <para>Generator plan phase 1 D5 — the <b>parse-level</b> <c>[ScopeChannel]</c> participant scan, written
    /// once. A body "hosts a participant" when any call reachable from its output chains resolves to an extension
    /// carrying <c>[ScopeChannel]</c>; the hosting body then provisions a <c>ScopeLocals</c> frame so
    /// <c>Scope.Publish</c>/<c>Scope.TryRead</c> have somewhere to live.</para>
    /// <para>The walk is the parse-tree twin of the runtime's compiled-tree
    /// <c>RuntimeDocument.ComputeNeedsLocals</c>: <b>every</b> item of a chain, recursing into nested chain
    /// parameters — not just the leftmost item. Per-side knowledge (a symbol attribute read on the build tier, a
    /// reflected attribute with inheritance on the run tier) enters through <paramref name="hasScopeChannel"/>.</para>
    /// <para><b>Documented over-provision (Q1.4, ruling: keep).</b> This scan runs <i>before</i> definition
    /// resolution, so a definition shadowing a <c>[ScopeChannel]</c> extension name is still counted as a
    /// participant. Over-provisioning only ever adds a frame nothing reads — publish/read happens exclusively
    /// inside <c>[ScopeChannel]</c> participants — so it is behavior-invisible and emit-time only. The tightening
    /// (a <c>definitionExists</c> predicate) is deferred to the named trigger in the plan's OQ4.</para>
    /// </summary>
    internal static class ParticipantScan
    {
        /// <summary>True when any output chain of <paramref name="context"/> hosts a participant.</summary>
        internal static bool BodyHostsParticipant(ParseContext context, Func<string, bool> hasScopeChannel)
        {
            var chains = context?.OutputChains;
            if (chains == null)
                return false;
            for (int i = 0; i < chains.Count; i++)
            {
                if (ChainHostsParticipant(chains[i], hasScopeChannel))
                    return true;
            }

            return false;
        }

        /// <summary>True when any item of <paramref name="chain"/> — or of any chain nested in one of its call
        /// parameters — resolves to a <c>[ScopeChannel]</c> extension.</summary>
        internal static bool ChainHostsParticipant(OutputChain chain, Func<string, bool> hasScopeChannel)
            => ItemsHostParticipant(chain?.Chain, hasScopeChannel);

        private static bool ItemsHostParticipant(List<OutputItem> items, Func<string, bool> hasScopeChannel)
        {
            if (items == null)
                return false;
            for (int i = 0; i < items.Count; i++)
            {
                if (ItemHostsParticipant(items[i], hasScopeChannel))
                    return true;
            }

            return false;
        }

        private static bool ItemHostsParticipant(OutputItem item, Func<string, bool> hasScopeChannel)
        {
            if (item == null)
                return false;
            var name = item.ExtensionName;
            if (!string.IsNullOrEmpty(name) && hasScopeChannel(name))
                return true;
            // The runtime's ChainedParameter recursion (RuntimeDocument.ItemNeedsLocals): a participant reachable
            // only as a nested chain parameter — @yell(@row()) — still provisions the hosting body's frame.
            return ItemsHostParticipant(item.CallParameter?.ChainParameter, hasScopeChannel);
        }
    }
}
