using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Runtime.Parameters;
using Heddle.Strings;
using Heddle.Strings.Core;

namespace Heddle.Runtime {
    internal class RuntimeDocument : IDataProcessor {
        private readonly DataProcessor[] _optimizedElements;
        private readonly IDataProcessor _singleProcessor;
        private readonly bool _canDoFullOptimize;
        private readonly CompileScope _context;

        public RuntimeDocument(string document, DocumentElement[] executeItems, CompileScope context)
        {
            Document = document;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            NeedsLocals = ComputeNeedsLocals(executeItems);
            var optimizedElements = OptimizeCallTree(executeItems, document, out _canDoFullOptimize);
            if (optimizedElements.Count == 1)
            {
                _singleProcessor = optimizedElements.First();
            }
            _optimizedElements = GetDocumentPieces(optimizedElements, document);
            if (_optimizedElements.Length == 1)
            {
                if (_optimizedElements[0].Piece != null)
                    Strategy = new DocumentStrategy(_optimizedElements[0].Piece);
                else
                    Strategy = new SingleStrategy(_optimizedElements[0].Processor);
            }
            else
            {
                if (_canDoFullOptimize)
                    Strategy = new OptimizedStrategy(_optimizedElements);
                else
                    Strategy = new NormalStrategy(_optimizedElements);
            }
        }

        private struct DataProcessor
        {
            public IDataProcessor Processor;
            public string Piece;
        }

        private static ICollection<IDataProcessor> OptimizeCallTree(DocumentElement[] items, string document, out bool canDoFullOptimize)
        {
            if (items == null || items.Length == 0)
            {
                canDoFullOptimize = false;
                return new IDataProcessor[0];
            }
            List<IDataProcessor> resultTree = new List<IDataProcessor>();
            if (items.Length == 1 && items[0].CallChain.Count == 1)
            {
                var singleProcessor = items[0].CallChain.ItemsToExecute[0];
                singleProcessor.Position = items[0].Position;
                if (document.Length == items[0].Position.Length &&
                    items[0].CallChain.ItemsToExecute[0].ReturnType == typeof(string))
                {
                    canDoFullOptimize = true;
                }
                else
                {
                    canDoFullOptimize = false;
                }
                return new IDataProcessor[] {singleProcessor};
            }
            int totalLength = 0;
            foreach (var item in items)
            {
                totalLength += item.Position.Length;
                if (item.CallChain.Count == 1)
                {
                    IDataProcessor resultItem = item.CallChain.ItemsToExecute[0];
                    resultItem.Position = item.Position;
                    resultTree.Add(resultItem);
                }
                else
                {
                    IDataProcessor resultItem = item.CallChain;
                    resultItem.Position = item.Position;
                    resultTree.Add(resultItem);
                }
            }
            canDoFullOptimize = totalLength == document.Length;
            return resultTree;
        }

        private static DataProcessor[] GetDocumentPieces(ICollection<IDataProcessor> processors, string document)
        {
            List<DataProcessor> optimized = new List<DataProcessor>();
            int offset = 0;
            foreach (var element in processors)
            {
                if (element.Position.StartIndex > offset)
                {
                    optimized.Add(new DataProcessor
                    {
                        Piece = document.Substring(offset, element.Position.StartIndex - offset),
                    });
                    optimized.Add(new DataProcessor
                    {
                        Processor = element
                    });
                }
                else
                {
                    optimized.Add(new DataProcessor
                    {
                        Processor = element
                    });
                }
                offset = element.Position.StartIndex + element.Position.Length;
            }
            if (document.Length > offset)
            {
                optimized.Add(new DataProcessor
                {
                    Piece = document.Substring(offset),
                    Processor = null
                });
            }
            return optimized.ToArray();
        }

        /// <summary>
        /// <para>Whether a body execution of this document must be provisioned with a
        /// <see cref="ScopeLocals"/> frame: <c>true</c> iff the compiled document statically contains a
        /// <c>[ScopeChannel]</c> participant (phase 3 D2). Nested bodies are separate documents and do not
        /// contribute — the flag is strictly per body level.</para>
        /// <para>Computed once in the constructor over the pre-optimization element tree (recursing nested
        /// chain parameters); immutable afterwards — safe to read from concurrent renders.</para>
        /// </summary>
        internal bool NeedsLocals { get; }

        private static bool ComputeNeedsLocals(DocumentElement[] items)
        {
            if (items == null)
                return false;
            foreach (var item in items)
            {
                if (ChainNeedsLocals(item.CallChain))
                    return true;
            }

            return false;
        }

        private static bool ChainNeedsLocals(TemplateChain chain)
        {
            if (chain == null)
                return false;
            foreach (var item in chain.ItemsToExecute)
            {
                if (ItemNeedsLocals(item))
                    return true;
            }

            return false;
        }

        private static bool ItemNeedsLocals(TemplateItem item)
        {
            if (item == null)
                return false;
            // Phase 8 (D4 carrier-transparency): a parameter-declaring [ScopeChannel] extension stands behind the
            // attribute-less ExtensionParameterCarrier — unwrap so its body still provisions a locals frame.
            var extension = (item.Extension as Core.ExtensionParameterCarrier)?.Inner ?? item.Extension;
            if (extension != null &&
                extension.GetType().IsHaveAttribute<ScopeChannelAttribute>(true))
                return true;
            if (item.Parameter is ChainedParameter chained)
            {
                switch (chained.Processor)
                {
                    case TemplateItem nestedItem:
                        return ItemNeedsLocals(nestedItem);
                    case TemplateChain nestedChain:
                        return ChainNeedsLocals(nestedChain);
                }
            }

            return false;
        }

        public IProcessStrategy Strategy { get; }

        public object ProcessData(in Scope scope) => Strategy.Execute(scope);

        public void RenderData(in Scope scope)
        {
            Strategy.Render(scope);
        }

        public BlockPosition Position { get; set; }

        public bool Empty => _optimizedElements.Count(e => e.Processor != null) == 0;

        public bool CanOptimizeSelf => _singleProcessor != null && _canDoFullOptimize;

        public IDataProcessor SingleProcessor => _singleProcessor;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var processor in _optimizedElements)
                {
                    processor.Processor?.Dispose();
                }
                _context.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        public string Document { get; }

        public void Dispose()
        {
            Dispose(true);
        }

        ~RuntimeDocument()
        {
            Dispose(false);
        }

        private sealed class SingleStrategy : IProcessStrategy
        {
            private readonly IDataProcessor _processor;

            public SingleStrategy(IDataProcessor processor)
            {
                _processor = processor;
            }

            public string Execute(in Scope scope) => _processor.ProcessData(scope) as string ?? string.Empty;

            public void Render(in Scope scope)
            {
                _processor.RenderData(scope);
            }
        }

        private sealed class DocumentStrategy : IProcessStrategy
        {
            private readonly string _document;

            public DocumentStrategy(string document)
            {
                _document = document;
            }

            public string Execute(in Scope scope) => _document;

            public void Render(in Scope scope)
            {
                scope.Renderer.Render(_document);
            }
        }

        private sealed class OptimizedStrategy : IProcessStrategy
        {
            private readonly IDataProcessor[] _processors;

            public OptimizedStrategy(DataProcessor[] processors)
            {
                _processors = processors.Select(p => p.Processor).ToArray();
            }

            public string Execute(in Scope scope)
            {
                var results = new string[_processors.Length];
                var index = 0;
                var totalLength = 0;
                foreach (var processor in _processors)
                {
                    var result = processor.ProcessData(scope) as string ?? string.Empty;
                    results[index] = result;
                    totalLength += result.Length;
                    index++;
                }

                return ExStringBuilder.Concat(results, _processors.Length, totalLength);
            }

            public void Render(in Scope scope)
            {
                foreach (var processor in _processors)
                {
                    processor.RenderData(scope);
                }
            }
        }

        private sealed class NormalStrategy : IProcessStrategy
        {
            private readonly DataProcessor[] _processors;

            public NormalStrategy(DataProcessor[] processors)
            {
                _processors = processors;
            }

            public string Execute(in Scope scope)
            {
                var results = new string[_processors.Length];
                var finalIndex = 0;
                var totalLength = 0;
                foreach (var element in _processors)
                {
                    var result = element.Processor?.ProcessData(scope) as string ?? element.Piece ?? string.Empty;

                    results[finalIndex] = result;
                    totalLength += result.Length;
                    finalIndex++;
                }

                return ExStringBuilder.Concat(results, _processors.Length, totalLength);
            }

            public void Render(in Scope scope)
            {
                foreach (var element in _processors)
                {
                    if (element.Piece != null)
                    {
                        scope.Renderer.Render(element.Piece);
                    }
                    else
                    {
                        element.Processor.RenderData(scope);
                    }
                }
            }
        }

    }
}
