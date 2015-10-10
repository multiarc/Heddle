using System.Collections.Generic;
using Templates.Collections;
using Templates.Strings.Core;

namespace Templates.Language
{
    public class DefinitionBlock
    {
        public SmartList<BlockPosition> Positions { get; }

        public DefinitionBlock(DefinitionBlock definitions = null)
        {
            Positions = new SmartList<BlockPosition>();
            Definitions = new Dictionary<string, DefinitionItem>();
            if (definitions?.Definitions != null)
            {
                foreach (var pair in definitions.Definitions)
                {
                    Definitions.Add(pair.Key, pair.Value);
                }
            }
        }

        public void AddNewBlockPosition(BlockPosition position)
        {
            Positions.Add(position);
        }

        public Dictionary<string, DefinitionItem> Definitions { get; }
    }
}