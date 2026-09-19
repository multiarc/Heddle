using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Runtime
{
    public class CompileScope: IDisposable
    {
        public CompileScope(CompileContext compileContext, CSharpContext csharpContext = null)
        {
            CompileContext = compileContext;
            CSharpContext = csharpContext ?? new CSharpContext();
        }

        public List<HeddleCompileError> CompileErrors => CompileContext.CompileErrors;

        /// <summary>Generated-site state for one compiled-form materialization. Null on every ordinary
        /// compile; set by the loader before replaying the row, read by the site creation points.</summary>
        internal Precompiled.SiteTableState SiteTableState { get; set; }

        public List<HeddleCompileWarning> CompileWarnings => CompileContext.CompileWarnings;

        public ICollection<string> Namespaces => CSharpContext.Namespaces;

        public ExType ScopeType
        {
            get => CompileContext.ScopeType;
            set => CompileContext.ScopeType = value;
        }

        public ExType RootScopeType
        {
            get => CompileContext.RootScopeType;
            set => CompileContext.RootScopeType = value;
        }

        public TemplateOptions Options => CompileContext.Options;

        public CompileContext CompileContext { get; }
        public CSharpContext CSharpContext { get; }

        /// <summary>The form cursor serving recorded bodies while materializing a compiled form. Null on
        /// every ordinary compile; when armed, <c>AbstractExtension.InitSubTemplate</c> compiles the recorded
        /// body instead of re-reading source. Carried by reference into nested body scopes.</summary>
        internal FormCursor FormCursor { get; set; }

        public void Dispose()
        {
            CompileContext?.Dispose();
        }
    }
}
