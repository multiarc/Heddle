using System;
using System.Runtime.Serialization;
using Templates.Strings.Core;

namespace Templates.Exceptions {
    [Serializable]
    public class TemplateInitException: Exception {
        public TemplateInitException ()
        {
        }

        public TemplateInitException (string message)
            : base(message)
        {
        }

        public TemplateInitException (string message, Exception inner)
            : base(message, inner)
        {
        }
        public TemplateInitException(string message, Exception inner, Position position)
            : base(message, inner)
        {
            Position = position;
        }

        protected TemplateInitException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public Position Position
        {
            get;
            private set;
        }

        public override void GetObjectData (SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("position", Position);
        }
    }
}