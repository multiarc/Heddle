using System;

namespace Heddle.Attributes {
    /// <summary>Excludes a property from template member resolution: a member path naming it does not
    /// resolve on either tier, and the editor does not offer it. Matched by full name, so only this
    /// attribute hides.</summary>
    [AttributeUsage (AttributeTargets.Property)]
    public sealed class HiddenAttribute: Attribute {
    }
}