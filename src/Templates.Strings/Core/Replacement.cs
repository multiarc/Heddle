using System;

namespace Templates.Strings.Core {
    /// <summary>
    /// Represents parsed result data to replace source template template
    /// </summary>
    [Serializable]
    public struct Replacement {
        public Position Position;

        /// <summary>
        /// Replacement string
        /// </summary>
        public string ReplacementValue;
    }
}