using System;
#if !DNXCORE50
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !DNXCORE50
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
#if !DNXCORE50
        protected TemplateProcessingException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}