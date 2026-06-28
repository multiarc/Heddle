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
            return _processStrategy != null ? _processStrategy.Execute(scope) : _innerResult;
        }

        protected void RenderInnerResult(in Scope scope)
        {
            if (_processStrategy != null)
            {
                _processStrategy.Render(scope);
            }
            else
            {
                scope.Renderer.Render(_innerResult);
            }
        }

        protected bool InnerExist => _subTemplate != null;

        public virtual void SetUpRenderType(RenderType renderType)
        {
            DirectRender = renderType == RenderType.Encode;
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
            return type;
        }

        public virtual void CompleteInit(CompileScope newScope, ParseContext parseContext)
        {

        }

        /// <summary>
        /// Implementation with parameter redirection in function
        /// </summary>
        /// <param name="scope"></param>
        /// <returns></returns>
        public abstract object ProcessData(in Scope scope);

        public abstract void RenderData(in Scope scope);

        public BlockPosition Position { get; set; }
    }
}