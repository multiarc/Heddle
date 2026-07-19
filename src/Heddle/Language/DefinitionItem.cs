using System;
using System.Collections.Generic;
using Heddle.Strings.Core;

namespace Heddle.Language {
    /// <summary>
    /// Definition syntax element with inheritance support
    /// </summary>
    public class DefinitionItem {
        internal DefinitionItem(DefinitionItem definition)
        {
            if (definition.BaseDefinition != null)
                BaseDefinition = new DefinitionItem(definition.BaseDefinition);
            Context = definition.Context;
            Position = definition.Position;
            ModelType = definition.ModelType;
            Name = definition.Name;
            ParameterTemplate = definition.ParameterTemplate;
            HasDefaultOutput = definition.HasDefaultOutput;
            PropDeclarations = definition.PropDeclarations;
            SlotTypeName = definition.SlotTypeName;
            IsRegion = definition.IsRegion;
            IsPublicRegion = definition.IsPublicRegion;
            Regions = definition.Regions;
            IsFillCandidate = definition.IsFillCandidate;
        }

        public DefinitionItem(string name, string parameterTemplate, DefinitionItem baseDefinition, string modelType = null)
        {
            FullOverride = name == baseDefinition?.Name;
            Name = name;
            ParameterTemplate = parameterTemplate;
            BaseDefinition = baseDefinition;
            ModelType = modelType?.Trim() ?? "object";
            Context = new ParseContext();
        }

        public void OverrideWith(DefinitionItem item)
        {
            BaseDefinition = new DefinitionItem(this);
            Position = item.Position;
            Context = item.Context;
            Name = item.Name;
            ParameterTemplate = item.ParameterTemplate;
            ModelType = item.ModelType;
            PropDeclarations = item.PropDeclarations;
            SlotTypeName = item.SlotTypeName;
        }

        /// <summary>
        /// Declared props of this declaration layer, inheritance not flattened (base props live on
        /// <see cref="BaseDefinition"/>). Empty for a header without a prop list. Set by the parser.
        /// </summary>
        public IReadOnlyList<PropDeclaration> PropDeclarations { get; internal set; } = Array.Empty<PropDeclaration>();

        /// <summary>
        /// The declared slot parameter type name (<c>out:: Type</c>), or <c>null</c> when the definition does
        /// not parameterize its slot. Set by the parser.
        /// </summary>
        public string SlotTypeName { get; internal set; }

        /// <summary>
        /// True when this declaration layer carried a default output (<c>-&gt; chain</c>). Preserved across
        /// full overrides — the default chain declared by an earlier layer keeps rendering at document end, so
        /// the double-render warning (HED4002) stays accurate. Set by the parser; read-only for hosts.
        /// </summary>
        public bool HasDefaultOutput { get; internal set; }

        /// <summary>
        /// Phase 7 D2: true for a definition declared inside a component body — a named content region
        /// (public via <c>&lt;:name&gt;</c> or a private inner <c>&lt;name&gt;</c>). Never true for a
        /// document-scope definition. Copied by the copy ctor; deliberately NOT part of
        /// <see cref="OverrideWith"/>'s field-copy list (the sibling idiom must not clobber region-ness).
        /// </summary>
        internal bool IsRegion { get; set; }

        /// <summary>Phase 7 D2: the <c>&lt;:name&gt;</c> public-region form. Meaningful only when
        /// <see cref="IsRegion"/> is true.</summary>
        internal bool IsPublicRegion { get; set; }

        /// <summary>
        /// Phase 7 D3: the directly-declared regions of this component's body (declaration order), appended on
        /// the <c>EnterDef</c> store-success path only, so a rejected duplicate never lands here. Empty for a
        /// region-less definition — the region table is additive metadata (D9).
        /// </summary>
        internal System.Collections.Generic.IReadOnlyList<RegionDeclaration> Regions { get; set; } =
            System.Array.Empty<RegionDeclaration>();

        /// <summary>
        /// Phase 7 D5: true for a call-body <c>&lt;x:x&gt;</c> whose base is unresolved — a captured
        /// <see cref="RegionFillCandidate"/>. Such an item is never registered into any
        /// <see cref="DefinitionBlock"/> (it must not self-shadow the region default a self-call resolves to).
        /// </summary>
        internal bool IsFillCandidate { get; set; }

        public bool FullOverride { get; set; }
        public DefinitionItem BaseDefinition { get; private set; }

        public BlockPosition Position { get; set; }
        public ParseContext Context { get; set; }
        public string Name { get; private set; }
        public string ParameterTemplate { get; private set; }
        public string ModelType { get; private set; }
    }
}