using System;
using Templates.Strings.Core;

namespace Templates.Data
{
    public static class CompileError {
        public static TtlCompileError ToError(this string errorMessage, BlockPosition position = default (BlockPosition)) {
            return new TtlCompileError
            {
                Error = errorMessage,
                Position = position
            };
        }

        public static TtlCompileError ToError(this Exception exception, BlockPosition position = default(BlockPosition)) {
            return new TtlCompileError
            {
                Error = exception.Message,
                Exception = exception,
                Position = position
            };
        }
    }
}