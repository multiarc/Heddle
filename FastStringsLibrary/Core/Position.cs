using System;
namespace FastStrings.Core {
    /// <summary>
    /// Represents posision of the template string to replace
    /// </summary>
    [Serializable]
    public struct Position {
        public readonly int Length;
        public readonly int StartIndex;

        public Position (int startIndex, int length)
        {
            StartIndex = startIndex;
            Length = length;
        }
    }
}