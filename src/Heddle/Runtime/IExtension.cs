using System;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.Runtime {
    /// <summary>
    /// Interface that should be implemented in every Template
    /// </summary>
    public interface IExtension: IDisposable {
        void SetUpRenderType(RenderType renderType);

        ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent);

        void CompleteInit(CompileScope newScope, ParseContext parseContext);

        object ProcessData(in Scope scope);
        
        void RenderData(in Scope scope);

        BlockPosition Position { get; set; }
    }
}