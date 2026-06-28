using System.Collections.Generic;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    public class DefinitionBlock
    {
        public List<BlockPosition> Positions { get; }

        public DefinitionBlock(DefinitionBlock definitions = null)
        {
            Positions = new List<BlockPosition>();
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