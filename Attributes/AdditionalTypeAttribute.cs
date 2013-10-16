using System;

namespace Templates.Attributes {
    /// <summary>
    /// Attribute to set additional data type goes to template extension
    /// </summary>
    [AttributeUsage (AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class AdditionalTypeAttribute: TypeAttribute {
        /// <summary>
        /// Sets additional data Type to template extension
        /// </summary>
        /// <param name="dataType">Additional Data Type</param>
        public AdditionalTypeAttribute (Type dataType)
            : base(dataType)
        {
        }
    }
}