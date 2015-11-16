using System;

namespace Templates.Attributes {
    /// <summary>
    /// Attribute to mark every extension with name wich used in source template
    /// This attribute must set to any extension class
    /// </summary>
    [AttributeUsage (AttributeTargets.All, AllowMultiple = true)]
    public sealed class ExtensionNameAttribute: Attribute {
        /// <summary>
        /// Sets extension name
        /// </summary>
        /// <param name="name">Extension name (empty string is reserved for <see cref="EmptyHtmlExtension"/>)</param>
        public ExtensionNameAttribute (string name)
        {
            Name = name;
        }

        public string Name
        {
            get;
            private set;
        }
    }
}