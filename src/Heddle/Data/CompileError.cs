using System;
using Heddle.Strings.Core;

namespace Heddle.Data
{
    public static class CompileError {
        public static HeddleCompileError ToError(this string errorMessage, BlockPosition position) {
            return new HeddleCompileError
            {
                Error = errorMessage,
                Position = position
            };
        }

        public static HeddleCompileError ToError(this Exception exception, BlockPosition position) {
            return new HeddleCompileError
            {
                Error = exception.Message,
                Exception = exception,
                Position = position
            };
        }
    }
}