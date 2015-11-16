using System;
#if !DOTNET5_4
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !DOTNET5_4
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
#if !DOTNET5_4
        protected TemplateCreateException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}