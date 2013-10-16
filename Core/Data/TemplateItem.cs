using System;

namespace Templates.Core.Data {
    internal class TemplateItem {
        public TemplateItem (Type returnType, IExtension extension)
        {
            ReturnType = returnType;
            Extension = extension;
        }

        public Type ReturnType
        {
            get;
            private set;
        }

        public IExtension Extension
        {
            get;
            private set;
        }
    }
}