using System;

namespace Heddle.Attributes {
    [AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ChainedTypeAttribute: Attribute {
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