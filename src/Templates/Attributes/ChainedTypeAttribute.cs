using System;

namespace Templates.Attributes {
    /// <summary>
    /// Attribute to set additional data type goes to template extension
    /// </summary>
    [AttributeUsage (AttributeTargets.Class)]
    public sealed class ChainedTypeAttribute: DataTypeAttribute {
        /// <summary>
        /// Sets additional data Type to template extension
        /// </summary>
        /// <param name="dataType">Additional Data Type</param>
        public ChainedTypeAttribute (Type dataType)
            : base(dataType)
        {
        }
    }
}