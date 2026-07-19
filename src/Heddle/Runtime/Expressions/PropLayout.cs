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

        /// <summary>Phase 8 (D5): true iff <paramref name="extensionType"/> (or a base — <c>[Prop]</c> is
        /// <c>Inherited = true</c>) declares at least one extension parameter. The cheap gate the relaxed
        /// HED5005 check consults without building the full layout.</summary>
        internal static bool DeclaresExtensionParameters(Type extensionType)
        {
            return extensionType != null && extensionType.IsHaveAttribute<Attributes.PropAttribute>(true);
        }

        /// <summary>
        /// Phase 8 (D1/D4/WI2): resolves the prop layout of a parameter-declaring extension from its
        /// <c>[Prop]</c> attributes — the new population source for the one prop contract. Walks the base-type
        /// chain outermost-first (base slots keep their indices, mirroring <see cref="Resolve"/>); re-raises the
        /// declaration-side ids (<c>HED5007</c>/<c>HED5008</c>/<c>HED5009</c>/<c>HED5010</c>/<c>HED5015</c>)
        /// positioned at <paramref name="ownerCallPosition"/> (the first call site — the extension's attribute
        /// source is C#, with no template position). <paramref name="ownerDisplay"/> is the complete owner noun
        /// phrase (<c>extension '&lt;name&gt;'</c>) the messages interpolate.
        /// </summary>
        internal static PropLayout ResolveFromExtension(Type extensionType, CompileScope compileScope,
            string ownerDisplay, BlockPosition ownerCallPosition)
        {
            var slots = new List<PropSlot>();
            var byName = new Dictionary<string, PropSlot>(StringComparer.Ordinal);

            // Base-type chain, outermost (deepest base) first — [Prop] is Inherited = true, so a subclass layers
            // its own declarations over its base's, exactly as a derived definition does.
            var layers = new List<Type>();
            for (var t = extensionType; t != null && t != typeof(object); t = t.BaseType)
                layers.Add(t);
            layers.Reverse();

            foreach (var layer in layers)
            {
                var attrs = layer.GetCustomAttributes(typeof(Attributes.PropAttribute), inherit: false);
                var seenAtLevel = new HashSet<string>(StringComparer.Ordinal);
                foreach (Attributes.PropAttribute attr in attrs)
                {
                    var name = attr.Name;

                    // A null/empty/whitespace name is not a usable parameter name (the attribute source admits
                    // what the definition grammar cannot) — the name-validity fault class, HED5015's row (D6).
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        compileScope.CompileErrors.Add(
                            $"A [Prop] parameter name on {ownerDisplay} is null or empty."
                                .ToError(ownerCallPosition, HeddleDiagnosticIds.ReservedPropName));
                        continue;
                    }

                    // The two ParseContext def-header pre-checks, re-raised over the attribute source (D6).
                    if (name == "out" || name == "this")
                    {
                        compileScope.CompileErrors.Add(
                            $"'{name}' is reserved and cannot be used as a prop name."
                                .ToError(ownerCallPosition, HeddleDiagnosticIds.ReservedPropName));
                        continue;
                    }

                    if (!seenAtLevel.Add(name))
                    {
                        compileScope.CompileErrors.Add(
                            $"Prop '{name}' is declared more than once on {ownerDisplay}."
                                .ToError(ownerCallPosition, HeddleDiagnosticIds.DuplicatePropDeclaration));
                        continue;
                    }

                    // An unusable Type (null / open generic / pointer / by-ref) cannot type a parameter (HED5010;
                    // the typeof() argument makes an unresolved *name* impossible — D6 correction).
                    var clrType = attr.Type;
                    if (clrType == null || clrType.ContainsGenericParameters || clrType.IsPointer || clrType.IsByRef)
                    {
                        compileScope.CompileErrors.Add(
                            $"Cannot resolve type for prop '{name}' of {ownerDisplay}."
                                .ToError(ownerCallPosition, HeddleDiagnosticIds.UnresolvedPropType));
                        continue;
                    }

                    ExType type = clrType;
                    bool hasDefault = attr.Default != null || attr.Optional;
                    if (byName.TryGetValue(name, out var existing))
                    {
                        // Inherited re-declaration: keep the base slot index; the re-declared type must be
                        // assignable to the inherited type, else HED5008 (the shared byName rule).
                        if (!existing.Type.Type.IsType(type.Type))
                        {
                            compileScope.CompileErrors.Add(
                                $"Prop '{name}' is re-declared with type {type.Type}, which is not assignable to the inherited type {existing.Type.Type}."
                                    .ToError(ownerCallPosition, HeddleDiagnosticIds.PropRedeclarationMismatch));
                            continue;
                        }

                        existing.Type = type;
                        existing.Position = ownerCallPosition;
                        ApplyDefaultCore(name, hasDefault, attr.Default, type, existing, ownerCallPosition,
                            compileScope);
                    }
                    else
                    {
                        var slot = new PropSlot
                        {
                            Name = name,
                            Type = type,
                            Index = slots.Count,
                            Position = ownerCallPosition
                        };
                        ApplyDefaultCore(name, hasDefault, attr.Default, type, slot, ownerCallPosition,
                            compileScope);
                        slots.Add(slot);
                        byName.Add(name, slot);
                    }
                }
            }

            return new PropLayout(slots, byName);
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
            ApplyDefaultCore(decl.Name, decl.HasDefault, decl.DefaultValue, type, slot, decl.Position, compileScope);
        }

        /// <summary>The D2 default-conversion core shared by the definition path (<see cref="Resolve"/>) and the
        /// phase 8 extension path (<see cref="ResolveFromExtension"/>) — one conversion rule, one HED5009 site.</summary>
        private static void ApplyDefaultCore(string name, bool hasDefault, object defaultValue, ExType type,
            PropSlot slot, BlockPosition position, CompileScope compileScope)
        {
            if (!hasDefault)
            {
                slot.HasDefault = false;
                slot.DefaultBoxed = null;
                return;
            }

            bool isNull = defaultValue == null;
            if (!PropConversion.TryConvertLiteral(defaultValue, isNull, type.Type, out var converted))
            {
                var literalType = defaultValue?.GetType().Name ?? "null";
                compileScope.CompileErrors.Add(
                    $"The default value for prop '{name}' ({literalType}) is not convertible to {type.Type}."
                        .ToError(position, HeddleDiagnosticIds.PropDefaultNotConvertible));
                slot.HasDefault = false;
                slot.DefaultBoxed = null;
                return;
            }

            slot.HasDefault = true;
            slot.DefaultBoxed = converted;
        }
    }
}
