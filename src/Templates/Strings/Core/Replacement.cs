#if !NETSTANDARD1_6
using System;
using System.Runtime.InteropServices;

#endif

namespace Templates.Strings.Core {
    /// <summary>
    /// Represents parsed result data to replace source template template
    /// </summary>
#if !NETSTANDARD1_6
    [Serializable]
#endif
    public struct Replacement
    {
        public BlockPosition BlockPosition;

        /// <summary>
        /// Replacement string
        /// </summary>
        public string ReplacementValue;
    }
}