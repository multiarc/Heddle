using System;
#if !DNXCORE50
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !DNXCORE50
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
#if !DNXCORE50
        protected TemplateFileException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}