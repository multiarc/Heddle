using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Templates.Data;

namespace Templates.Runtime
{
    public class CompileScope: IDisposable
    {
        public CompileScope(CompileContext compileContext, CSharpContext cSharpContext = null)
        {
            CompileContext = compileContext;
            CSharpContext = cSharpContext ?? new CSharpContext();
        }

        public List<TtlCompileError> CompileErrors => CompileContext.CompileErrors;

        public List<TtlCompileWarning> CompileWarnings => CompileContext.CompileWarnings;

        public ICollection<string> Namespaces => CSharpContext.Namespaces;

        public ExType ScopeType
        {
            get { return CompileContext.ScopeType; }
            set { CompileContext.ScopeType = value; }
        }

        public ExType RootScopeType
        {
            get { return CompileContext.RootScopeType; }
            set { CompileContext.RootScopeType = value; }
        }

        public TemplateOptions Options => CompileContext.Options;

        public CompileContext CompileContext { get; }
        public CSharpContext CSharpContext { get; }

        public void Dispose()
        {
            CompileContext?.Dispose();
        }
    }
}
