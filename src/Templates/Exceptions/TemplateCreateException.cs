using System;
using System.Runtime.Serialization;

namespace Templates.Exceptions {
#if !DNXCORE50
    [Serializable]
#endif
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
#if !DNXCORE50
        protected TemplateCreateException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}