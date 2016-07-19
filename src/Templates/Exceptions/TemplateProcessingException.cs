using System;
#if !NETSTANDARD1_6
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !NETSTANDARD1_6
    [Serializable]
#endif
    public class TemplateProcessingException: Exception {
        public TemplateProcessingException ()
        {
        }

        public TemplateProcessingException (string message)
            : base(message)
        {
        }

        public TemplateProcessingException (string message, Exception inner)
            : base(message, inner)
        {
        }
#if !NETSTANDARD1_6
        protected TemplateProcessingException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}