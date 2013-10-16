using System;

namespace Templates.Attributes {
    /// <summary>
    /// Attribute to mark extension be directly renderable to page, that means output data should be encoded by HTML encoder by default
    /// </summary>
    [AttributeUsage (AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class DirectRenderAttribute: Attribute {
    }
}