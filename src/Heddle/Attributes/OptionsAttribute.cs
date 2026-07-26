using System;

namespace Heddle.Attributes {
    [AttributeUsage (AttributeTargets.All, Inherited = false)]
    public sealed class OptionsAttribute: Attribute {
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