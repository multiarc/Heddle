using System;

namespace Heddle.Attributes {
    /// <summary>
    /// Attribute to set additional data type goes to template extension
    /// </summary>
    [AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ChainedTypeAttribute: Attribute {
        /// <summary>
        /// Sets additional data Type to template extension
        /// </summary>
        /// <param name="dataType">Additional Data Type</param>
        public ChainedTypeAttribute(Type dataType)
        {
            DataType = dataType;
        }

        public Type DataType
        {
            get;
            private set;
        }
    }
}