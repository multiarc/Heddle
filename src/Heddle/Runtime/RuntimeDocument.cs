using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language;
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

            /// <summary>
            /// <see cref="Piece"/> pre-encoded as UTF-8, so a UTF-8 sink writes final-form bytes
            /// instead of re-transcoding the same static text on every render. Null when this
            /// element carries a <see cref="Processor"/> rather than a literal piece. Encoded once
            /// at document build: templates compile once, so the cost is off the render path.
            /// </summary>
            public byte[] PieceUtf8;
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

        /// <summary>Segments document into static pieces and processors; shared with emitter so <c>P0..Pn</c> constants match.</summary>
        private static DataProcessor[] GetDocumentPieces(ICollection<IDataProcessor> processors, string document)
        {
            List<DataProcessor> optimized = new List<DataProcessor>();
            DocumentShaping.SlicePieces(processors, element => element.Position, document,
                piece => optimized.Add(new DataProcessor { Piece = piece, Processor = null }),
                element =>
                {
                    optimized.Add(new DataProcessor { Processor = element });
                    return true;
                });
            return optimized.ToArray();
        }

        /// <summary>
        /// Whether a body execution must provision a <see cref="ScopeLocals"/> frame: <c>true</c> iff the document
        /// contains a <c>[ScopeChannel]</c> participant. Computed once, immutable and thread-safe.
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
            // Unwrap ExtensionParameterCarrier to reach the [ScopeChannel] extension it wraps.
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

            // Guard: if extension cannot produce a string, degrade to empty.
            public string Execute(in Scope scope) => _processor.ProcessData(scope) as string ?? string.Empty;

            public void Render(in Scope scope)
            {
                _processor.RenderData(scope);
            }
        }

        private sealed class DocumentStrategy : IProcessStrategy
        {
            private readonly string _document;
            private readonly byte[] _documentUtf8;

            public DocumentStrategy(string document)
            {
                _document = document;
                _documentUtf8 = Encoding.UTF8.GetBytes(document);
            }

            public string Execute(in Scope scope) => _document;

            // Same shape as PrecompiledRuntime.WritePiece, whose semantics the parity gates pin: a
            // UTF-8 sink takes pre-encoded bytes, everything else takes the chars. Static pieces
            // reach the sink directly and never pass through the encode proxy, so the
            // never-bypass-encoding invariant is untouched.
            public void Render(in Scope scope)
            {
                if (scope.Renderer is IUtf8ScopeRenderer u8) u8.RenderUtf8(_documentUtf8);
                else scope.Renderer.Render(_document);
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
                // Eager: templates compile once, so this is one-time work off the render path. Costs
                // roughly the static content again in bytes for ASCII-dominated templates, which is
                // the trade for not re-transcoding every static piece on every UTF-8 render.
                for (var i = 0; i < _processors.Length; i++)
                    if (_processors[i].Piece != null)
                        _processors[i].PieceUtf8 = Encoding.UTF8.GetBytes(_processors[i].Piece);
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
                // Type-tested once for the whole document rather than per piece.
                var u8 = scope.Renderer as IUtf8ScopeRenderer;
                for (var i = 0; i < _processors.Length; i++)
                {
                    var element = _processors[i];
                    if (element.Piece != null)
                    {
                        if (u8 != null) u8.RenderUtf8(element.PieceUtf8);
                        else scope.Renderer.Render(element.Piece);
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
