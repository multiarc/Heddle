using System;

namespace Templates.Exceptions {
    public class TemplateProcessingException: Exception {
        public TemplateProcessingException ()
        {
        }

        public TemplateProcessingException (string message)
            : base(message)
        {
        }

        public TemplateProcessingException (string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}