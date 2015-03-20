using System;

namespace Templates.Data
{
    public static class CompileError {
        public static TtlCompileError ToError(this string errorMessage) {
            return new TtlCompileError
            {
                Error = errorMessage
            };
        }

        public static TtlCompileError ToError(this Exception exception) {
            return new TtlCompileError
            {
                Error = exception.Message,
                Exception = exception
            };
        }
    }
}