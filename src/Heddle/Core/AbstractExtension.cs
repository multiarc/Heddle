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
            // Phase 3 D2/D5: install the body's local-context frame. A participating body gets a fresh
            // frame; a non-participating body under a provisioned parent gets a cleared (null) frame so it
            // never sees the parent's; otherwise the incoming scope passes through unchanged (fast path).
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
        /// Phase 7 D5 (<c>PrecompiledRuntime.Bind</c>): installs a generated body on this pre-constructed extension,
        /// reproducing what <see cref="InitStart"/> does for a subtemplate — store the body strategy, apply the
        /// render type, record the frame-provisioning flag, and set the source position — without a
        /// <c>RuntimeDocument</c>. Called once, from a generated static initializer (thread-safe via CLR type-init);
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
        /// <para>Computes this extension's chained/output value for <paramref name="scope"/>. Return a
        /// <see cref="string"/> when the extension has a textual value to contribute; return
        /// <see cref="string.Empty"/> when it has no textual value here (e.g. a render-only extension,
        /// or a directive that produces no output).</para>
        /// <para>The value/string rail coerces any non-<see cref="string"/> result to empty output
        /// (<c>as string ?? string.Empty</c>). This is a deliberate guard — it keeps a stray object's
        /// default <c>ToString()</c> from leaking into concatenated output — but it also silently drops
        /// an otherwise-meaningful boxed scalar (e.g. an <see cref="int"/> or a <see cref="System.Guid"/>)
        /// returned here instead of a string. Built-in formatters (<c>@int</c>, <c>@string</c>,
        /// <c>@guid</c>, …) already stringify at their own boundary before returning, so they are safe.
        /// Stringify at your own boundary too: do not rely on the rail to convert a non-string value for
        /// you.</para>
        /// </summary>
        /// <param name="scope">The current render scope.</param>
        /// <returns>A <see cref="string"/> textual value, or <see cref="string.Empty"/> when this
        /// extension has no textual value to contribute.</returns>
        public abstract object ProcessData(in Scope scope);

        public abstract void RenderData(in Scope scope);

        public BlockPosition Position { get; set; }
    }
}