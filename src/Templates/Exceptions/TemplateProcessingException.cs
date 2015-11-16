using System;
#if !DOTNET5_4
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !DOTNET5_4
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
#if !DOTNET5_4
        protected TemplateProcessingException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}