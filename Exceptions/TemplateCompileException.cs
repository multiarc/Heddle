using System;
using System.Runtime.Serialization;

namespace Templates.Exceptions {
    [Serializable]
    public class TemplateCompileException: Exception {
        public TemplateCompileException ()
        {
        }

        public TemplateCompileException (string message)
            : base(message)
        {
        }

        public TemplateCompileException (string message, Exception inner)
            : base(message, inner)
        {
        }

        protected TemplateCompileException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}