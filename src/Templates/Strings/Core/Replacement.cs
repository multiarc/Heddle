#if !NETSTANDARD1_5
using System;
#endif

namespace Templates.Strings.Core {
    /// <summary>
    /// Represents parsed result data to replace source template template
    /// </summary>
#if !NETSTANDARD1_5
    [Serializable]
#endif
    public struct Replacement {
        public BlockPosition BlockPosition;

        /// <summary>
        /// Replacement string
        /// </summary>
        public string ReplacementValue;
    }
}