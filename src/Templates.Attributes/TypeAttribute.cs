using System;

namespace Templates.Attributes {
    /// <summary>
    /// Attribute to set main data type goes to template extension
    /// </summary>
    [AttributeUsage (AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class TypeAttribute: Attribute {
        /// <summary>
        /// Sets main data Type to template extension
        /// </summary>
        /// <param name="dataType">Main data Type</param>
        public TypeAttribute (Type dataType)
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