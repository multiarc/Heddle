using System;
#if !DOTNET5_4
using System.Runtime.Serialization;
#endif
using Templates.Strings.Core;

namespace Templates.Exceptions {
#if !DOTNET5_4
    [Serializable]
#endif
    public class TemplateDefinitionTypeException: Exception {
        public BlockPosition Position { get; }

        public TemplateDefinitionTypeException(BlockPosition position)
        {
            Position = position;
        }

        public TemplateDefinitionTypeException(BlockPosition position, string message) : this(message)
        {
            Position = position;
        }

        public TemplateDefinitionTypeException(BlockPosition position, string message, Exception innerException)
            : this(message, innerException)
        {
            Position = position;
        }

        public TemplateDefinitionTypeException(string message) : base(message) {
        }

        public TemplateDefinitionTypeException(string message, Exception innerException) : base(message, innerException) {
        }
#if !DOTNET5_4
        protected TemplateDefinitionTypeException(SerializationInfo info, StreamingContext context) : base(info, context) {
            base.GetObjectData(info, context);
            info.AddValue("Position", Position);
        }
#endif
    }
}
