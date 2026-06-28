using System;
using Heddle.Strings.Core;

namespace Heddle.Exceptions {
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
    }
}