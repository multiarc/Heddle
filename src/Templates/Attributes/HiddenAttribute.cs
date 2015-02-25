using System;

namespace Templates.Attributes {
    /// <summary>
    /// Prevents data property from vision in source template
    /// </summary>
    [AttributeUsage (AttributeTargets.Property)]
    public sealed class HiddenAttribute: Attribute {
    }
}