using System;

namespace Heddle.Attributes {
    [AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
    public class DataTypeAttribute: Attribute {
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