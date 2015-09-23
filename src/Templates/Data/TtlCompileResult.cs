using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Templates.Data
{
    public sealed class TtlCompileResult
    {
        public TtlCompileResult(bool success)
        {
            Success = success;
            Errors = new List<TtlCompileError>();
        }

        public bool Success { get; }

        public List<TtlCompileError> Errors { get; set; }

        public IReadOnlyCollection<TtlCompileError> ErrorList => new ReadOnlyCollection<TtlCompileError>(Errors);

        public override string ToString()
        {
            return Errors.Aggregate("", (s, error) => $"{s}{error.ToString()}\r\n");
        }
    }
}