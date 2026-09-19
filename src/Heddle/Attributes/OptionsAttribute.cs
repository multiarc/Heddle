using System;

namespace Heddle.Attributes {
    /// <summary>Names a member the way template source refers to it. The engine resolves members by
    /// their declared names and does not read this attribute; it remains for binary compatibility.</summary>
    [AttributeUsage (AttributeTargets.All, Inherited = false)]
    public sealed class OptionsAttribute: Attribute {
        /// <summary>Records <paramref name="fieldName"/> as the template-facing name.</summary>
        public OptionsAttribute (string fieldName)
        {
            FieldName = fieldName;
        }

        public string FieldName
        {
            get;
            private set;
        }
    }
}