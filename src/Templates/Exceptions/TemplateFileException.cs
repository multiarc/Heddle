using System;
#if !NETSTANDARD1_5
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !NETSTANDARD1_5
    [Serializable]
#endif
    public class TemplateFileException: Exception {
        public TemplateFileException ()
        {
        }

        public TemplateFileException (string message)
            : base(message)
        {
        }

        public TemplateFileException (string message, Exception inner)
            : base(message, inner)
        {
        }
#if !NETSTANDARD1_5
        protected TemplateFileException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}