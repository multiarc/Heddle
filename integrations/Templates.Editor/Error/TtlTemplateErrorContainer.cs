using System;

namespace Templates.Editor.Error {
    internal sealed class TtlTemplateErrorContainer {
        public Exception Exception { get; set; }
        public string Message { get; set; }

        public TtlTemplateErrorContainer(Exception exception, string message)
        {
            Exception = exception;
            Message = message + "\r\n" + exception.Message;
        }
    }
}
