using System;
using System.Runtime.Serialization;

namespace Templates.Exceptions {
    /// <summary>
    /// Parse Exception, at any stage can be raised
    /// </summary>
#if !DNXCORE50
    [Serializable]
#endif
    public class TemplateParseException: Exception {
        public TemplateParseException ()
        {
        }

        public TemplateParseException (string message)
            : base(message)
        {
        }

        public TemplateParseException (string message, Exception inner)
            : base(message, inner)
        {
        }
#if !DNXCORE50
        protected TemplateParseException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}