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
            Definitions = definitions?.Definitions == null
                ? new Dictionary<string, DefinitionItem>()
                : new Dictionary<string, DefinitionItem>(definitions.Definitions);
        }

        public void AddNewBlockPosition(BlockPosition position)
        {
            Positions.Add(position);
        }

        public Dictionary<string, DefinitionItem> Definitions { get; }
    }
}