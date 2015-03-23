using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Templates.Data
{
    public sealed class TtlCompileResult
    {
        internal TtlCompileResult(bool success)
        {
            Success = success;
            Errors = new List<TtlCompileError>();
        }

        public bool Success { get; }

        internal List<TtlCompileError> Errors { get; set; }

        public IReadOnlyCollection<TtlCompileError> ErrorList => new ReadOnlyCollection<TtlCompileError>(Errors);
    }
}