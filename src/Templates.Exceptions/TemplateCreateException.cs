using System;
using System.Runtime.Serialization;

namespace Templates.Exceptions {
    [Serializable]
    public class TemplateCreateException: Exception {
        public TemplateCreateException ()
        {
        }

        public TemplateCreateException (string message)
            : base(message)
        {
        }

        public TemplateCreateException (string message, Exception inner)
            : base(message, inner)
        {
        }

        protected TemplateCreateException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}