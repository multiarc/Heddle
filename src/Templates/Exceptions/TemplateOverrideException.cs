using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Templates.Exceptions {
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

        protected TemplateOverrideException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
