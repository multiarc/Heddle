using System;
#if !NETSTANDARD1_5
using System.Runtime.Serialization;
#endif

namespace Templates.Exceptions {
#if !NETSTANDARD1_5
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
#if !NETSTANDARD1_5
        protected TemplateOverrideException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
