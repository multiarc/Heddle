using System;
using System.Collections.Generic;
using System.Linq;
#if !DNXCORE50
using System.Runtime.Serialization;
#endif
using Templates.Data;

namespace Templates.Exceptions {
#if !DNXCORE50
    [Serializable]
#endif
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

        public TemplateCompileException (string message, Exception inner, IEnumerable<TtlCompileError> errors)
            : base(message, inner)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Errors = new List<TtlCompileError>(errors);
        }

        public override string ToString()
        {
            if (Errors != null && Errors.Count > 0)
                return Errors.Aggregate("", (s, error) => $"{s}\r\n{error.ToString()}");
            return base.ToString();
        }

#if !DNXCORE50
        protected TemplateCompileException (SerializationInfo info, StreamingContext context, IEnumerable<TtlCompileError> errors)
            : base(info, context)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Errors = new List<TtlCompileError>(errors);
            base.GetObjectData(info, context);
            info.AddValue("Errors", Errors);
        }
#endif
    }
}