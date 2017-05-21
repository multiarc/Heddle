using System;
using System.Collections.Generic;
using System.Linq;
using Templates.Data;

namespace Templates.Exceptions {
    public class TemplateCompileException: Exception {
        public List<TtlCompileError> Errors { get; }

        public TemplateCompileException (IEnumerable<TtlCompileError> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Errors = new List<TtlCompileError>(errors);
        }

        public TemplateCompileException(TtlCompileError error) : base(error.Error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            Errors = new List<TtlCompileError>{error};
        }

        public TemplateCompileException (string message, IEnumerable<TtlCompileError> errors)
            : base(message)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Errors = new List<TtlCompileError>(errors);
        }

        public TemplateCompileException(string message, Exception inner, TtlCompileError error)
            : base(message, inner)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            Errors = new List<TtlCompileError> {error};
        }

        public override string ToString()
        {
            if (Errors != null && Errors.Count > 0)
                return Errors.Aggregate("", (s, error) => $"{s}\r\n{error.ToString()}");
            return base.ToString();
        }

    }
}