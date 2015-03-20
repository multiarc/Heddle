using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Templates.Data;

namespace Templates.Exceptions {
    [Serializable]
    public class TemplateCompileException: Exception {
        public List<TtlCompileError> Errors { get; }

        public TemplateCompileException (IEnumerable<TtlCompileError> errors)
        {
            if (errors == null) throw new ArgumentNullException("errors");
            Errors = new List<TtlCompileError>(errors);
        }

        public TemplateCompileException(TtlCompileError error) : base(error.Error)
        {
            if (error == null) throw new ArgumentNullException("error");
            Errors = new List<TtlCompileError>{error};
        }

        public TemplateCompileException (string message, IEnumerable<TtlCompileError> errors)
            : base(message)
        {
            if (errors == null) throw new ArgumentNullException("errors");
            Errors = new List<TtlCompileError>(errors);
        }

        public TemplateCompileException (string message, Exception inner, IEnumerable<TtlCompileError> errors)
            : base(message, inner)
        {
            if (errors == null) throw new ArgumentNullException("errors");
            Errors = new List<TtlCompileError>(errors);
        }

        protected TemplateCompileException (SerializationInfo info, StreamingContext context, IEnumerable<TtlCompileError> errors)
            : base(info, context)
        {
            if (errors == null) throw new ArgumentNullException("errors");
            Errors = new List<TtlCompileError>(errors);
            base.GetObjectData(info, context);
            info.AddValue("Errors", Errors);
        }
    }
}