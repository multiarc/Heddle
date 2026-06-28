using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;

namespace Heddle.Exceptions {
    public class TemplateCompileException: Exception {
        public List<HeddleCompileError> Errors { get; }

        public TemplateCompileException (IEnumerable<HeddleCompileError> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Errors = new List<HeddleCompileError>(errors);
        }

        public TemplateCompileException(HeddleCompileError error) : base(error.Error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            Errors = new List<HeddleCompileError>{error};
        }

        public TemplateCompileException (string message, IEnumerable<HeddleCompileError> errors)
            : base(message)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Errors = new List<HeddleCompileError>(errors);
        }

        public TemplateCompileException(string message, Exception inner, HeddleCompileError error)
            : base(message, inner)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            Errors = new List<HeddleCompileError> {error};
        }

        public override string ToString()
        {
            if (Errors != null && Errors.Count > 0)
                return Errors.Aggregate("", (s, error) => $"{s}\r\n{error.ToString()}");
            return base.ToString();
        }

    }
}