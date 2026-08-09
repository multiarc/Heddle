using System;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle.Core
{
    public abstract class AbstractExtension : IExtension
    {
        protected bool DirectRender;
        private string _innerResult = string.Empty;
        private RuntimeDocument _subTemplate;
        private IProcessStrategy _processStrategy;
        private bool _needsLocals;
        private bool _hasPrecompiledBody;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _subTemplate?.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        protected string GetInnerResult(in Scope scope)
        {
            if (_processStrategy == null)
                return _innerResult;
            // Avoid creating scope locals when the body doesn't need them (fast path).
            if (_needsLocals)
                return _processStrategy.Execute(scope.WithLocals(new ScopeLocals()));
            if (scope.Locals != null)
                return _processStrategy.Execute(scope.WithLocals(null));
            return _processStrategy.Execute(scope);
        }

        protected void RenderInnerResult(in Scope scope)
        {
            if (_processStrategy == null)
            {
                scope.Renderer.Render(_innerResult);
                return;
            }
            if (_needsLocals)
            {
                _processStrategy.Render(scope.WithLocals(new ScopeLocals()));
            }
            else if (scope.Locals != null)
            {
                _processStrategy.Render(scope.WithLocals(null));
            }
            else
            {
                _processStrategy.Render(scope);
            }
        }

        protected bool InnerExist => _subTemplate != null || _hasPrecompiledBody;

        public virtual void SetUpRenderType(RenderType renderType)
        {
            DirectRender = renderType == RenderType.Encode;
        }

        /// <summary>
        /// Install a precompiled body without a <c>RuntimeDocument</c>. Thread-safe via CLR type-init;
        /// the extension is never mutated after it returns.
        /// </summary>
        internal void BindPrecompiled(IProcessStrategy body, RenderType renderType, bool needsLocals,
            BlockPosition position)
        {
            _processStrategy = body;
            _needsLocals = needsLocals;
            _hasPrecompiledBody = body != null;
            SetUpRenderType(renderType);
            Position = position;
        }

        private static ExType InitSubTemplate(ref string parameterTemplate, ExType dataType, ExType chainedType,
            CompileScope compileScope,
            ParseContext parseContext, out RuntimeDocument result)
        {
            if (compileScope == null)
                throw new ArgumentNullException(nameof(compileScope));

            RuntimeDocument subTemplate;
            if (string.IsNullOrEmpty(parameterTemplate))
                subTemplate = null;
            else
            {
                var newContext = new CompileScope(new CompileContext(compileScope.CompileContext, dataType), compileScope.CSharpContext);
                subTemplate = HeddleCompiler.Compile(parameterTemplate, newContext, parseContext, chainedType);
                newContext.CompileContext.Compile();
            }

            if (subTemplate != null)
            {
                parameterTemplate = subTemplate.Document;
            }

            if (subTemplate == null || subTemplate.Empty)
            {
                result = null;
            }
            else
            {
                result = subTemplate;
            }

            return typeof(string);
        }

        public virtual ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            if (PrecompiledBodySupply.TryConsume(dataType, chainedType, ref initContext.ParameterTemplate,
                out var supplied))
            {
                if (supplied.AssignInnerResult)
                    _innerResult = supplied.InnerResult;
                _subTemplate = null;
                _processStrategy = supplied.Strategy;
                _needsLocals = supplied.NeedsLocals;
                _hasPrecompiledBody = supplied.Strategy != null;
                return typeof(string);
            }

            var type = InitSubTemplate(ref initContext.ParameterTemplate, dataType, chainedType, initContext.CompileScope,
                initContext.ParseContext, out var subTemplate);
            if (subTemplate == null)
                _innerResult = initContext.ParameterTemplate;
            _subTemplate = subTemplate;
            _processStrategy = subTemplate?.Strategy;
            _needsLocals = subTemplate?.NeedsLocals ?? false;
            return type;
        }

        public virtual void CompleteInit(CompileScope newScope, ParseContext parseContext)
        {

        }

        /// <summary>
        /// Computes this extension's value for <paramref name="scope"/>. Return a <see cref="string"/>, or <see cref="string.Empty"/> for no output.
        /// Any non-string result is coerced to empty (prevents stray <c>ToString()</c> leakage); stringify at your own boundary instead.
        /// </summary>
        /// <param name="scope">The current render scope.</param>
        /// <returns>A <see cref="string"/>, or <see cref="string.Empty"/>.</returns>
        public abstract object ProcessData(in Scope scope);

        public abstract void RenderData(in Scope scope);

        public BlockPosition Position { get; set; }
    }
}