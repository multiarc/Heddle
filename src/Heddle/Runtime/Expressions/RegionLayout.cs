using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.Runtime.Expressions
{
    /// <summary>One resolved named-region slot of a component (phase 7 D3), mirroring <see cref="PropSlot"/>.
    /// <see cref="Definition"/> is the region's own definition (its default body) as seen by the component
    /// instance the layout was resolved from; the compile-time fill step re-fetches the per-call-site instance
    /// from the callee's isolated body context.</summary>
    internal sealed class RegionSlot
    {
        internal string Name;
        internal bool IsPublic;            // <:name> => true; a component's private inner <name> => false
        internal ExType ModelType;         // resolved <:name :: Type>; null when unresolvable/abstract
        internal string ModelTypeName;     // the unresolved name, for diagnostics
        internal BlockPosition Position;   // the declaration site
        internal DefinitionItem Definition;
    }

    /// <summary>
    /// The phase 7 D3 named-region table: a flattened, index-stable, ordinally-keyed lookup of a component's
    /// directly-declared regions, resolved once per component and cached in
    /// <c>CompileContext.ResolvedRegionLayouts</c> (the <see cref="PropLayout"/> precedent). It does NOT detect
    /// duplicates — a duplicate <c>&lt;:name&gt;</c> is rejected at parse (HED5020) and never stored (F4a). The
    /// anonymous <c>@out()</c> slot is the table's implicit default entry and keeps its existing machinery: the
    /// table holds only the named regions, and a region-less definition has an empty table (no region code runs —
    /// the D9 byte-identity anchor).
    /// </summary>
    internal sealed class RegionLayout
    {
        private readonly List<RegionSlot> _slots;
        private readonly Dictionary<string, RegionSlot> _byName;

        private RegionLayout(List<RegionSlot> slots, Dictionary<string, RegionSlot> byName)
        {
            _slots = slots;
            _byName = byName;
        }

        internal IReadOnlyList<RegionSlot> Slots => _slots;

        internal int Count => _slots.Count;

        /// <summary>Compile-time only; render never sees a region name.</summary>
        internal bool TryGet(string name, out RegionSlot slot) => _byName.TryGetValue(name, out slot);

        internal static RegionLayout Resolve(DefinitionItem component, CompileScope scope)
        {
            var slots = new List<RegionSlot>();
            var byName = new Dictionary<string, RegionSlot>(StringComparer.Ordinal);
            foreach (var declaration in component.Regions)
            {
                ExType modelType = null;
                try
                {
                    var resolved = ReflectionHelper.ResolveType(declaration.TypeName, scope.CSharpContext.Namespaces);
                    if (resolved != null)
                        modelType = new ExType(resolved);
                }
                catch (InvalidOperationException)
                {
                    // Unresolvable region model type: left null here; the ordinary definition-compile path
                    // raises the positioned resolution error when (and only when) the region is rendered.
                }

                DefinitionItem definition = null;
                component.Context?.DefinitionsBlock?.Definitions.TryGetValue(declaration.Name, out definition);
                var slot = new RegionSlot
                {
                    Name = declaration.Name,
                    IsPublic = declaration.IsPublic,
                    ModelType = modelType,
                    ModelTypeName = declaration.TypeName,
                    Position = declaration.Position,
                    Definition = definition
                };
                slots.Add(slot);
                byName[declaration.Name] = slot;
            }

            return new RegionLayout(slots, byName);
        }
    }

    /// <summary>
    /// The phase 7 D4 call-scoped fill scope: a small immutable <c>regionName → materialized-fill
    /// DefinitionItem</c> map carried on <c>CompileContext</c> and copied into every nested body-compile by the
    /// child-context copy ctor (the proven <c>ActivePropLayout</c> propagation seam), so a fill reaches a region
    /// call at any depth. Consulted by <c>HeddleCompiler.CompileItem</c> before the parse-context lookup.
    /// </summary>
    internal sealed class RegionFillScope
    {
        private readonly Dictionary<string, DefinitionItem> _fills;

        internal RegionFillScope(Dictionary<string, DefinitionItem> fills)
        {
            _fills = fills;
        }

        internal bool TryGet(string name, out DefinitionItem fill) => _fills.TryGetValue(name, out fill);

        /// <summary>The D4 step 5 self-reference variant: while compiling region <c>name</c>'s own (fill) body,
        /// the name resolves to <paramref name="target"/> (its base default) so a self-call terminates; a null
        /// target removes the entry (the base chain is exhausted — resolution falls back to the parse context).</summary>
        internal RegionFillScope WithRebind(string name, DefinitionItem target)
        {
            var copy = new Dictionary<string, DefinitionItem>(_fills, StringComparer.Ordinal);
            if (target == null)
                copy.Remove(name);
            else
                copy[name] = target;
            return new RegionFillScope(copy);
        }
    }
}
