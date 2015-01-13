using System;

namespace Templates.Strings.Core {
    /// <summary>
    /// Represents parsed result data to replace source template template
    /// </summary>
    [Serializable]
    public struct Replacement {
        public BlockPosition BlockPosition;

        /// <summary>
        /// Replacement string
        /// </summary>
        public string ReplacementValue;
    }
}