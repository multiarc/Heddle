using System;

namespace Heddle.Attributes {
    /// <summary>Declares a model property whose value is meant to bypass output encoding. The renderer
    /// decides encoding per extension (<see cref="EncodeOutputAttribute"/>, the profile and <c>@raw</c>)
    /// and does not read this attribute, so it has no effect on output; it remains for binary
    /// compatibility.</summary>
    [AttributeUsage (AttributeTargets.Property)]
    public sealed class NotEncodeAttribute: Attribute {
    }
}