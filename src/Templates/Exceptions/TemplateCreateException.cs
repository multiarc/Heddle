using System;
#if !NETSTANDARD1_5
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !NETSTANDARD1_5
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
#if !NETSTANDARD1_5
        protected TemplateCreateException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}