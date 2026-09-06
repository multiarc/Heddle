using System;
using Heddle.Data;
using Heddle.Language;
using Heddle.Precompiled.CompiledForm;
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
            ParseContext parseContext, OutputItem sourceItem, out RuntimeDocument result)
        {
            if (compileScope == null)
                throw new ArgumentNullException(nameof(compileScope));

            RuntimeDocument subTemplate;
            if (string.IsNullOrEmpty(parameterTemplate))
                subTemplate = null;
            else
            {
                var newContext = new CompileScope(new CompileContext(compileScope.CompileContext, dataType), compileScope.CSharpContext);
                // The form-cursor seam: while materializing, the body a hook requests is served from the
                // recorded form (with its consumed types checked) instead of the hook's text. Hooks run
                // unchanged over the supplied body; the cursor is carried into the nested scope so deeper
                // bodies resolve against the served document. Nothing is armed on the dynamic tier, where
                // the slot and the ambient are both null.
                var cursor = compileScope.FormCursor ?? FormCursor.Current;
                newContext.FormCursor = cursor;
                string bodyText = parameterTemplate;
                bool served = cursor != null && sourceItem != null &&
                    cursor.TryServeBody(sourceItem.Position, parameterTemplate, dataType, chainedType,
                        out bodyText);
                try
                {
                    subTemplate = HeddleCompiler.Compile(bodyText, newContext, parseContext, chainedType);
                    newContext.CompileContext.Compile();
                }
                finally
                {
                    if (served)
                        cursor.ExitBody();
                }
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

            var rawTemplate = initContext.ParameterTemplate;
            var type = InitSubTemplate(ref initContext.ParameterTemplate, dataType, chainedType, initContext.CompileScope,
                initContext.ParseContext, initContext.SourceItem, out var subTemplate);
            var record = initContext.CompileScope?.CompileContext.FormRecord;
            if (record != null && initContext.SourceItem != null && !string.IsNullOrEmpty(rawTemplate))
            {
                record.RecordBody(initContext.SourceItem, rawTemplate, initContext.ParameterTemplate,
                    dataType, chainedType, record.GetDocIndex(subTemplate));
            }

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