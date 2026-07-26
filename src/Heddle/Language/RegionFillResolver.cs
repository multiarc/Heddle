using System;
using System.Collections.Generic;

namespace Heddle.Language
{
    /// <summary>The per-candidate outcome of the region-fill matching rule (generator plan phase 2 D7). The
    /// <em>decision</em> is shared; the <em>reactions</em> are each backend's own — the runtime retracts and
    /// raises HED5019, the generator un-precompiles — which is why this is a verdict rather than a bool.</summary>
    internal enum RegionFillVerdict
    {
        /// <summary>A public region matched and its region default was found; the fill is materialized.</summary>
        Matched,

        /// <summary>The candidate names no region of the callee — genuinely dangling.</summary>
        Dangling,

        /// <summary>The named region exists but is not public, so it cannot be overridden from a call site.</summary>
        Private,

        /// <summary>The region is declared but its default definition is not stored (defensive).</summary>
        DefaultMissing
    }

    /// <summary>Abstracts the callee's region table: the runtime wraps its cached reflection-bound
    /// <c>RegionLayout</c>, the generator a flat ordinal scan over the parsed declarations.</summary>
    internal delegate bool TryLookupRegion(string name, out bool isPublic);

    /// <summary>
    /// <para>Generator plan phase 2 D7 — the call-site region-fill matching rule, written once. Both backends
    /// already shared the materialization leaf (<see cref="DefinitionMaterializer"/>); this is the four-step
    /// decision <em>around</em> it that they duplicated with different table representations: origin-identity
    /// filter → region lookup → public gate → region-default fetch → materialize.</para>
    /// <para>The lookup is only consulted for candidates that pass the origin filter, so a caller may resolve its
    /// region table lazily inside <paramref name="lookup"/> (the runtime does, keeping its
    /// resolve-only-on-first-origin-match behavior).</para>
    /// </summary>
    internal static class RegionFillResolver
    {
        internal static void Resolve(IReadOnlyList<RegionFillCandidate> candidates, ParseContext origin,
            TryLookupRegion lookup, DefinitionItem calleeDefinition,
            Action<RegionFillCandidate, RegionFillVerdict, DefinitionItem> onVerdict)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Origin != origin)
                    continue;

                if (!lookup(candidate.Name, out var isPublic))
                {
                    onVerdict(candidate, RegionFillVerdict.Dangling, null);
                    continue;
                }

                if (!isPublic)
                {
                    onVerdict(candidate, RegionFillVerdict.Private, null);
                    continue;
                }

                // The region default of THIS call site's isolated callee instance — the fill layers over it, so a
                // self-call inside the override body resolves to this site's own base default (D4 steps 4/5).
                DefinitionItem regionDefault = null;
                calleeDefinition?.Context?.DefinitionsBlock?.Definitions.TryGetValue(candidate.Name,
                    out regionDefault);
                if (regionDefault == null)
                {
                    onVerdict(candidate, RegionFillVerdict.DefaultMissing, null);
                    continue;
                }

                onVerdict(candidate, RegionFillVerdict.Matched,
                    DefinitionMaterializer.Materialize(candidate, regionDefault));
            }
        }
    }
}
