using System;
using System.Collections.Generic;
using Templates.Data;

namespace Templates.Runtime {
    internal class TemplateItem {
        public TemplateItem(Type returnType, IExtension extension)
        {
            ReturnType = returnType;
            Extension = extension;
        }

        public Type ReturnType
        {
            get;
            private set;
        }

        public RuntimeCallParameter Parameter { get; set; }

        public IExtension Extension
        {
            get;
            private set;
        }
    }
}