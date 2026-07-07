using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.Runtime.Expressions
{
    /// <summary>One resolved layout slot (D6). <see cref="DefaultBoxed"/> is the D2-converted value boxed once at
    /// resolution (<c>null</c> for both the null-literal default and a required prop — <see cref="HasDefault"/>
    /// disambiguates).</summary>
    internal sealed class PropSlot
    {
        internal string Name;
        internal ExType Type;
        internal bool HasDefault;
        internal object DefaultBoxed;
        internal int Index;
        internal BlockPosition Position;
    }

    /// <summary>
    /// The D6 flattened, index-stable prop table for one definition: base-chain props in declaration order
    /// (outermost base first), then this definition's new props. Re-declared inherited names keep their base
    /// index and re-default/narrow. <see cref="TryGet"/> is compile-time only (render never sees a name).
    /// </summary>
    internal sealed class PropLayout
    {
        private readonly List<PropSlot> _slots;
        private readonly Dictionary<string, PropSlot> _byName;

        private PropLayout(List<PropSlot> slots, Dictionary<string, PropSlot> byName)
        {
            _slots = slots;
            _byName = byName;
        }

        internal IReadOnlyList<PropSlot> Slots => _slots;

        internal int Count => _slots.Count;

        internal bool TryGet(string name, out PropSlot slot) => _byName.TryGetValue(name, out slot);

        /// <summary>
        /// D9 shadowing: emits HED5011 (warning) when a prop hit also names a readable, visible property of the
        /// current scope type. The prop still wins; the member is reachable via <c>this.&lt;name&gt;</c>.
        /// </summary>
        internal static void WarnIfShadowsMember(CompileScope compileScope, ExType scopeType, string name,
            BlockPosition position)
        {
            if (scopeType == null || scopeType.IsDynamic || scopeType.Type == null)
                return;
            var property = scopeType.Type.GetProperty(name, MemberPathResolver.MemberBindingFlags);
            if (!MemberPathResolver.IsAccessible(property))
                return;
            compileScope.CompileWarnings.Add(new HeddleCompileWarning
            {
                Error = $"Prop '{name}' hides the model member '{scopeType.Type}.{name}' — '{name}' reads the prop.",
                Fix = $"Rename the prop, or read the member explicitly with 'this.{name}' in an expression.",
                Position = position,
                DiagnosticId = HeddleDiagnosticIds.PropShadowsModelMember
            });
        }

        internal static PropLayout Resolve(DefinitionItem definition, CompileScope compileScope)
        {
            var slots = new List<PropSlot>();
            var byName = new Dictionary<string, PropSlot>(StringComparer.Ordinal);
            var namespaces = compileScope.CSharpContext.Namespaces;

            // Walk the inheritance chain outermost-first so base slots keep their indices in every descendant.
            var layers = new List<DefinitionItem>();
            for (var d = definition; d != null; d = d.BaseDefinition)
                layers.Add(d);
            layers.Reverse();

            foreach (var layer in layers)
            {
                foreach (var decl in layer.PropDeclarations)
                {
                    ExType type;
                    try
                    {
                        var resolved = ReflectionHelper.ResolveType(decl.TypeName, namespaces);
                        if (resolved == null)
                        {
                            compileScope.CompileErrors.Add(
                                $"Cannot resolve type '{decl.TypeName}' for prop '{decl.Name}' of definition '{definition.Name}'."
                                    .ToError(decl.Position, HeddleDiagnosticIds.UnresolvedPropType));
                            continue;
                        }

                        type = resolved;
                    }
                    catch (InvalidOperationException)
                    {
                        compileScope.CompileErrors.Add(
                            $"Cannot resolve type '{decl.TypeName}' for prop '{decl.Name}' of definition '{definition.Name}'."
                                .ToError(decl.Position, HeddleDiagnosticIds.UnresolvedPropType));
                        continue;
                    }

                    if (byName.TryGetValue(decl.Name, out var existing))
                    {
                        // Re-declaration: keep the base slot index; the re-declared type must be assignable to
                        // the inherited type (the same direction as model narrowing), else HED5008.
                        if (!existing.Type.Type.IsType(type.Type))
                        {
                            compileScope.CompileErrors.Add(
                                $"Prop '{decl.Name}' is re-declared with type {type.Type}, which is not assignable to the inherited type {existing.Type.Type}."
                                    .ToError(decl.Position, HeddleDiagnosticIds.PropRedeclarationMismatch));
                            continue;
                        }

                        existing.Type = type;
                        existing.Position = decl.Position;
                        ApplyDefault(decl, type, existing, compileScope);
                    }
                    else
                    {
                        var slot = new PropSlot
                        {
                            Name = decl.Name,
                            Type = type,
                            Index = slots.Count,
                            Position = decl.Position
                        };
                        ApplyDefault(decl, type, slot, compileScope);
                        slots.Add(slot);
                        byName.Add(decl.Name, slot);
                    }
                }
            }

            return new PropLayout(slots, byName);
        }

        private static void ApplyDefault(PropDeclaration decl, ExType type, PropSlot slot, CompileScope compileScope)
        {
            if (!decl.HasDefault)
            {
                slot.HasDefault = false;
                slot.DefaultBoxed = null;
                return;
            }

            bool isNull = decl.DefaultValue == null;
            if (!PropConversion.TryConvertLiteral(decl.DefaultValue, isNull, type.Type, out var converted))
            {
                var literalType = decl.DefaultValue?.GetType().Name ?? "null";
                compileScope.CompileErrors.Add(
                    $"The default value for prop '{decl.Name}' ({literalType}) is not convertible to {type.Type}."
                        .ToError(decl.Position, HeddleDiagnosticIds.PropDefaultNotConvertible));
                slot.HasDefault = false;
                slot.DefaultBoxed = null;
                return;
            }

            slot.HasDefault = true;
            slot.DefaultBoxed = converted;
        }
    }
}
