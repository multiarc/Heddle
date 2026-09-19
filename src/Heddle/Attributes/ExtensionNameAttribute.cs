using System;

namespace Heddle.Attributes {
    /// <summary>Names an extension the way templates call it: <c>@name(...)</c>. Every extension class
    /// carries one; a class may carry several to answer to more than one name.</summary>
    [AttributeUsage (AttributeTargets.All, AllowMultiple = true)]
    public sealed class ExtensionNameAttribute: Attribute {
        /// <param name="name">Extension name. The empty string <c>""</c> is the unnamed <c>@(...)</c> carrier:
        /// it resolves to <see cref="Heddle.Extensions.EmptyExtension"/> under
        /// <see cref="Heddle.Data.OutputProfile.Text"/> and is redirected to
        /// <see cref="Heddle.Extensions.EmptyHtmlExtension"/> (name <c>"html"</c>) under
        /// <see cref="Heddle.Data.OutputProfile.Html"/>. <c>"raw"</c> is a second name on
        /// <see cref="Heddle.Extensions.EmptyExtension"/> for the trusted-value opt-out.</param>
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