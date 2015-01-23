using System;
using Templates.Helpers;

namespace Templates.Runtime {
    public class TemplateItem {
        public TemplateItem(TypeReference returnType, IExtension extension)
        {
            ReturnType = returnType;
            Extension = extension;
        }

        public TypeReference ReturnType
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