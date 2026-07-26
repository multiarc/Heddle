using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Language.Binding;
using Heddle.Strings.Core;

namespace Heddle.Runtime.Expressions
{
    /// <summary>One resolved layout slot. <see cref="DefaultBoxed"/> is the converted value boxed once at
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
    /// The flattened, index-stable prop table for one definition: base-chain props in declaration order
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
        /// Shadowing: emits HED5011 (warning) when a prop hit also names a readable, visible property of the
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

        /// <summary>
        /// The extension's resolved slot layout as a single string — ordered
        /// <c>name:&lt;slot type AQN&gt;</c> pairs joined with <c>|</c> — for the manifest's prop-layout
        /// fingerprint row and the gauntlet check that compares it against the live extension type.
        /// <para>Built by the same shared <see cref="PropLayoutCore"/> the compile path uses, with faults
        /// discarded: a malformed declaration set is not this method's business (the compile path diagnoses it),
        /// and the fingerprint of whatever layout the core produces is exactly what the generator's frozen
        /// prototype was indexed against. Returns <c>null</c> for a parameter-less extension, which is what makes
        /// the gauntlet check vacuous where there is nothing to check.</para>
        /// </summary>
        internal static string Fingerprint(Type extensionType)
        {
            if (extensionType == null || !DeclaresExtensionParameters(extensionType))
                return null;

            var slots = PropLayoutCore.Build(ReadDeclarations(extensionType), ReflectionTypeFacts.Instance,
                DiscardingSink.Instance, out _);
            return PropLayoutCore.Fingerprint(slots, ReflectionTypeFacts.Instance);
        }

        private sealed class DiscardingSink : IPropLayoutSink<Type>
        {
            internal static readonly DiscardingSink Instance = new DiscardingSink();

            public void Fault(PropFault fault, PropDeclaration<Type> declaration, Type relatedType,
                string relatedDisplay)
            {
            }

            public bool TryConvertDefault(PropDeclaration<Type> declaration, Type targetType, out object converted,
                out string sourceDisplay)
            {
                sourceDisplay = null;
                return PropConversion.TryConvertLiteral(declaration.DefaultValue, declaration.DefaultValue == null,
                    targetType, out converted);
            }
        }

        /// <summary>True iff <paramref name="extensionType"/> (or a base — <c>[Prop]</c> is
        /// <c>Inherited = true</c>) declares at least one extension parameter. The cheap gate the relaxed
        /// HED5005 check consults without building the full layout.</summary>
        internal static bool DeclaresExtensionParameters(Type extensionType)
        {
            return extensionType != null && extensionType.IsHaveAttribute<Attributes.PropAttribute>(true);
        }

        /// <summary>
        /// Resolves the prop layout of a parameter-declaring extension from its
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
            // Both reflection and generator sides use the shared core for sequencing and indexing via their
            // respective adapters, preventing divergence. Decode the layers into declarations, feed through the
            // core with a reflection adapter, then re-shape the resulting slots.
            var declarations = ReadDeclarations(extensionType);
            var sink = new ReflectionPropSink(compileScope, ownerDisplay, ownerCallPosition);
            var built = PropLayoutCore.Build(declarations, ReflectionTypeFacts.Instance, sink, out _);

            var slots = new List<PropSlot>(built.Count);
            var byName = new Dictionary<string, PropSlot>(StringComparer.Ordinal);
            foreach (var slot in built)
            {
                var resolved = new PropSlot
                {
                    Name = slot.Name,
                    Type = slot.Type,
                    Index = slot.Index,
                    HasDefault = slot.HasDefault,
                    DefaultBoxed = slot.DefaultBoxed,
                    Position = ownerCallPosition
                };
                slots.Add(resolved);
                byName.Add(resolved.Name, resolved);
            }

            return new PropLayout(slots, byName);
        }

        /// <summary>The reflection side's layer walk: the base-type chain outermost (deepest base) first, stopping
        /// <b>at</b> <c>typeof(object)</c> (<see cref="PropLayoutCore.StopsAtObject"/>). <c>[Prop]</c> is
        /// <c>Inherited = true</c>, so a subclass layers its own declarations over its base's exactly as a derived
        /// definition does — which is why each layer is read with <c>inherit: false</c>.</summary>
        private static List<PropDeclaration<Type>> ReadDeclarations(Type extensionType)
        {
            var layers = new List<Type>();
            for (var t = extensionType; t != null && t != typeof(object); t = t.BaseType)
                layers.Add(t);
            layers.Reverse();

            var declarations = new List<PropDeclaration<Type>>();
            for (int level = 0; level < layers.Count; level++)
            {
                var attrs = layers[level].GetCustomAttributes(typeof(Attributes.PropAttribute), inherit: false);
                foreach (Attributes.PropAttribute attr in attrs)
                {
                    declarations.Add(new PropDeclaration<Type>
                    {
                        Name = attr.Name,
                        Type = attr.Type,
                        Level = level,
                        HasDefault = attr.Default != null || attr.Optional,
                        DefaultValue = attr.Default
                    });
                }
            }

            return declarations;
        }

        /// <summary>The dynamic tier's fault sink: every fault class maps to its shipped <c>HED50xx</c> id and the
        /// shared message text (<c>HeddleDiagnosticCatalog.PropFaults</c>), positioned at the first call site —
        /// the extension's attribute source is C# and has no template position.</summary>
        private sealed class ReflectionPropSink : IPropLayoutSink<Type>
        {
            private readonly CompileScope _compileScope;
            private readonly string _ownerDisplay;
            private readonly BlockPosition _position;

            internal ReflectionPropSink(CompileScope compileScope, string ownerDisplay, BlockPosition position)
            {
                _compileScope = compileScope;
                _ownerDisplay = ownerDisplay;
                _position = position;
            }

            public void Fault(PropFault fault, PropDeclaration<Type> declaration, Type relatedType,
                string relatedDisplay)
            {
                var message = HeddleDiagnosticCatalog.PropFaults.Message(fault, declaration.Name, _ownerDisplay,
                    ReflectionTypeFacts.Instance.Display(declaration.Type), relatedDisplay);
                _compileScope.CompileErrors.Add(
                    message.ToError(_position, HeddleDiagnosticCatalog.PropFaults.RuntimeDiagnosticId(fault)));
            }

            public bool TryConvertDefault(PropDeclaration<Type> declaration, Type targetType, out object converted,
                out string sourceDisplay)
            {
                bool isNull = declaration.DefaultValue == null;
                sourceDisplay = declaration.DefaultValue?.GetType().Name ?? "null";
                return PropConversion.TryConvertLiteral(declaration.DefaultValue, isNull, targetType, out converted);
            }
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

        /// <summary>The default-conversion core shared by the definition path (<see cref="Resolve"/>) and the
        /// extension path (<see cref="ResolveFromExtension"/>) — one conversion rule, one HED5009 site.</summary>
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
