using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Exceptions
{
    public sealed class TemplateParseException : TemplateCompileException
    {

        public TemplateParseException(IEnumerable<HeddleCompileError> errors) : base(errors)
        {
        }

        public TemplateParseException(HeddleCompileError error) : base(error)
        {
        }

        public TemplateParseException(string message, IEnumerable<HeddleCompileError> errors)
            : base(message, errors)
        {
        }

        public TemplateParseException(string message, Exception inner, HeddleCompileError error)
            : base(message, inner, error)
        {
        }
    }
}