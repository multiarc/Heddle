using System;

namespace Templates.Attributes {
    /// <summary>
    /// Prevents data property from HTML encoding
    /// </summary>
    [AttributeUsage (AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NotEncodeAttribute: Attribute {
    }
}