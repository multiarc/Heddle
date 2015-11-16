using System;
#if !DOTNET5_4
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !DOTNET5_4
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
#if !DOTNET5_4
        protected TemplateFileException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}