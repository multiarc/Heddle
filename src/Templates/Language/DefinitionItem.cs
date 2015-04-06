using System.Linq;
using Templates.Collections;
using Templates.Strings.Core;

namespace Templates.Language {
    /// <summary>
    /// Definition syntax element with inheritance support
    /// </summary>
    public class DefinitionItem {
        internal DefinitionItem(DefinitionItem definition)
        {
            BaseDefinition = definition.BaseDefinition;
            Context = definition.Context;
            Position = definition.Position;
            ModelType = definition.ModelType;
            Name = definition.Name;
            ParameterTemplate = definition.ParameterTemplate;
        }

        public DefinitionItem(string name, string parameterTemplate, DefinitionItem baseDefinition, string modelType = null)
        {
            FullOverride = name == baseDefinition?.Name;
            Name = name;
            ParameterTemplate = parameterTemplate;
            BaseDefinition = baseDefinition;
            ModelType = modelType ?? "object";
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
        }

        public bool FullOverride { get; set; }
        public DefinitionItem BaseDefinition { get; private set; }

        public BlockPosition Position { get; set; }
        public ParseContext Context { get; set; }
        public string Name { get; private set; }
        public string ParameterTemplate { get; private set; }
        public string ModelType { get; private set; }
    }
}