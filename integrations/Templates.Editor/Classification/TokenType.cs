using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Templates.Editor.Classification {
    internal enum TokenType {
        DefinitionStart,
        DefinitionEnd,
        DefinitionNameEnd,
        DefinitionNameStart,
        ExtensionDelimeter,
        ParameterStart,
        ValidIdentifier,
        ParameterEnd,
        Space,
        Other
    }
}
