using System;

namespace Heddle.Attributes {
    /// <summary>Declares a data type an extension accepts: the value the template passes it as its
    /// parameter. Repeat the attribute for each accepted type; an interface admits every
    /// implementation.</summary>
    [AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
    public class DataTypeAttribute: Attribute {
        /// <summary>Declares <paramref name="dataType"/> as a data type the extension accepts.</summary>
        public DataTypeAttribute (Type dataType)
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