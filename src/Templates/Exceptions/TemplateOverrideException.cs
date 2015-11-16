using System;
#if !DOTNET5_4
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !DOTNET5_4
    [Serializable]
#endif
    public class TemplateOverrideException : Exception {
        public TemplateOverrideException ()
        {
        }

        public TemplateOverrideException (string message)
            : base(message)
        {
        }

        public TemplateOverrideException (string message, Exception inner)
            : base(message, inner)
        {
        }
#if !DOTNET5_4
        protected TemplateOverrideException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
