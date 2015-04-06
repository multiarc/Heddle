using System;
using System.Runtime.Serialization;
using Templates.Strings.Core;

namespace Templates.Exceptions {
    /// <summary>
    /// Parse Exception, at any stage can be raised
    /// </summary>
#if !DNXCORE50
    [Serializable]
#endif
    public class TemplateParseException: Exception {

        public BlockPosition Position { get; }
        public TemplateParseException (BlockPosition position)
        {
            Position = position;
        }

        public TemplateParseException (string message, BlockPosition position)
            : base(message)
        {
            Position = position;
        }

        public TemplateParseException (string message, Exception inner, BlockPosition position)
            : base(message, inner)
        {
            Position = position;
        }

#if !DNXCORE50
        protected TemplateParseException (SerializationInfo info, StreamingContext context, BlockPosition position)
            : base(info, context)
        {
            Position = position;
        }
#endif
    }
}