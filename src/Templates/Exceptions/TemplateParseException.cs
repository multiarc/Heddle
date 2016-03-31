using System;
using System.Collections.Generic;
using Templates.Data;

namespace Templates.Exceptions
{
    /// <summary>
    /// Parse Exception, at any stage can be raised
    /// </summary>
#if !NETSTANDARD1_5
    [Serializable]
#endif
    public sealed class TemplateParseException : TemplateCompileException
    {

        public TemplateParseException(IEnumerable<TtlCompileError> errors) : base(errors)
        {
        }

        public TemplateParseException(TtlCompileError error) : base(error)
        {
        }

        public TemplateParseException(string message, IEnumerable<TtlCompileError> errors)
            : base(message, errors)
        {
        }

        public TemplateParseException(string message, Exception inner, TtlCompileError error)
            : base(message, inner, error)
        {
        }
    }
}