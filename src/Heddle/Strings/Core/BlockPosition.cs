using System;
using Antlr4.Runtime;

namespace Heddle.Strings.Core {
    /// <summary>
    /// Represents posision of the template string to replace
    /// </summary>
    public struct BlockPosition {
        public readonly int Length;
        public readonly int StartIndex;

        public BlockPosition (int startIndex, int length)
        {
            if (startIndex < 0 || length < 0)
                throw new ArgumentException();
            StartIndex = startIndex;
            Length = length;
        }

        public BlockPosition(IToken token)
        {
            StartIndex = token.StartIndex;
            Length = token.StopIndex - token.StartIndex + 1;
        }

        public override string ToString()
        {
            return $"{StartIndex}:{Length}";
        }
    }
}