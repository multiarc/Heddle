using System;
using System.Runtime.Serialization;

namespace Templates.Exceptions {
#if !DNXCORE50
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
#if !DNXCORE50
        protected TemplateOverrideException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
