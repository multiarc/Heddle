using System;
#if !DNXCORE50
using System.Runtime.Serialization;
#endif
using Templates.Strings.Core;

namespace Templates.Exceptions {
#if !DNXCORE50
    [Serializable]
#endif
    public class TemplateInitException: Exception {
        public BlockPosition BlockPosition { get; }

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
        public TemplateInitException(string message, Exception inner, BlockPosition blockPosition)
            : base(message, inner)
        {
            BlockPosition = blockPosition;
        }
#if !DNXCORE50
        protected TemplateInitException (SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public override void GetObjectData (SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("BlockPosition", BlockPosition);
        }
#endif
    }
}