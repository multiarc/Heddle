using System;

namespace Templates.Attributes {
    /// <summary>
    /// Attribute to set additional data type goes to template extension
    /// </summary>
    [AttributeUsage (AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class AdditionalDataTypeAttribute: DataTypeAttribute {
        /// <summary>
        /// Sets additional data Type to template extension
        /// </summary>
        /// <param name="dataType">Additional Data Type</param>
        public AdditionalDataTypeAttribute (Type dataType)
            : base(dataType)
        {
        }
    }
}