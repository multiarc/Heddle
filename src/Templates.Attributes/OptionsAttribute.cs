using System;

namespace Templates.Attributes {
    /// <summary>
    /// Attribute to mark data property to set it's internal name in source template
    /// </summary>
    [AttributeUsage (AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    public sealed class OptionsAttribute: Attribute {
        /// <summary>
        /// Sets data property name to use
        /// </summary>
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