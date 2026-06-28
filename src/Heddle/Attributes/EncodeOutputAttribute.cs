using System;

namespace Heddle.Attributes {
    /// <summary>
    /// Attribute to mark extension be directly renderable to page, that means output data should be encoded by HTML encoder by default
    /// </summary>
    [AttributeUsage (AttributeTargets.Class)]
    public sealed class EncodeOutputAttribute: Attribute {
    }
}