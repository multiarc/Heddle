using System;

namespace Heddle.Attributes {
    /// <summary>Declares a chained type an extension accepts: the value the preceding link of a
    /// <c>:</c> chain hands it. Repeat the attribute for each accepted type; an interface admits every
    /// implementation.</summary>
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