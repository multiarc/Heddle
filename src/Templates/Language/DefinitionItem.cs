using Templates.Collections;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Language {
    /// <summary>
    /// Definition syntax element with inheritance support
    /// </summary>
    public class DefinitionItem {
        public DefinitionItem(string name, string parameterTemplate, DefinitionItem baseDefinition, string modelType = null)
        {
            Name = name;
            ParameterTemplate = parameterTemplate;
            BaseDefinition = baseDefinition;
            ModelType = modelType ?? "object";
            Context = new ParseContext();
            OutList = new SmartList<OutputChain>();
        }

        public DefinitionItem BaseDefinition { get; private set; }

        public BlockPosition Position { get; set; }
        public ParseContext Context { get; set; }
        public string Name { get; private set; }
        public string ParameterTemplate { get; private set; }
        public string ModelType { get; private set; }
        public SmartList<OutputChain> OutList { get; set; }
    }
}