using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Exceptions
{
    /// <summary>
    /// Parse Exception, at any stage can be raised
    /// </summary>
#if !DNXCORE50
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

        public TemplateParseException(string message, Exception inner, IEnumerable<TtlCompileError> errors)
            : base(message, inner, errors)
        {
        }
    }
}