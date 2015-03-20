using System;

namespace Templates.Strings.Core {
    /// <summary>
    /// Represents posision of the template string to replace
    /// </summary>
    [Serializable]
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
    }
}