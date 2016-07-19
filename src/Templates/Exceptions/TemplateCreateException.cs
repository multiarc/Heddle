using System;
#if !NETSTANDARD1_6
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !NETSTANDARD1_6
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
#if !NETSTANDARD1_6
        protected TemplateCreateException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}