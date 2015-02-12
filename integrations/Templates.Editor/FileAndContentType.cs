using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor {
    internal static class FileAndContentType {
        [Export]
        [Name("ttl")]
        [BaseDefinition("htmlx")]
        internal static ContentTypeDefinition TtlContentTypeDefinition;

        [Export]
        [FileExtension(".thtml")]
        [ContentType("ttl")]
        internal static FileExtensionToContentTypeDefinition TtlFileExtensionToContentTypeDefinition;
    }
}