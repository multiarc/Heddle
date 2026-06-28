using System;

namespace Heddle.Attributes {
    /// <summary>
    /// Prevents data property from HTML encoding
    /// </summary>
    [AttributeUsage (AttributeTargets.Property)]
    public sealed class NotEncodeAttribute: Attribute {
    }
}