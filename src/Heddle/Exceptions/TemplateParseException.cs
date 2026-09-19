using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Exceptions
{
    /// <summary>Raised when the parser meets a construct it cannot continue past: a subtemplate closed
    /// before it opened, a definition without a name. Carries the positioned
    /// <see cref="Heddle.Data.HeddleCompileError"/> diagnostics collected up to that point. Every other
    /// template mistake is reported as a collected diagnostic, not thrown.</summary>
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