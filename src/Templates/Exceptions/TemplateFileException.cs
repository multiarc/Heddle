using System;
using System.Runtime.Serialization;

namespace Templates.Exceptions {
    [Serializable]
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

        protected TemplateFileException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}