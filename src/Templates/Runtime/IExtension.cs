using System;
using Templates.Core;
using Templates.Data;
using Templates.Language;
using Templates.Strings.Core;

namespace Templates.Runtime {
    /// <summary>
    /// Interface that should be implemented in every Template
    /// </summary>
    public interface IExtension: IDisposable {
        void SetUpRenderType(RenderType renderType);

        ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent);

        void CompleteInit(CompileContext newContext, ParseContext parseContext);

        object ProcessData(object data, object chained, object parent, object root);

        BlockPosition Position { get; set; }
    }
}