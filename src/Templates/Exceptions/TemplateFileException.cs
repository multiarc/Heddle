using System;
#if !NETSTANDARD1_6
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !NETSTANDARD1_6
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
#if !NETSTANDARD1_6
        protected TemplateFileException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}