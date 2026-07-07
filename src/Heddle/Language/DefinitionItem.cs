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

        public bool FullOverride { get; set; }
        public DefinitionItem BaseDefinition { get; private set; }

        public BlockPosition Position { get; set; }
        public ParseContext Context { get; set; }
        public string Name { get; private set; }
        public string ParameterTemplate { get; private set; }
        public string ModelType { get; private set; }
    }
}