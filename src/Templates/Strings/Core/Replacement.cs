#if !DNXCORE50
using System;
#endif

namespace Templates.Strings.Core {
    /// <summary>
    /// Represents parsed result data to replace source template template
    /// </summary>
#if !DNXCORE50
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