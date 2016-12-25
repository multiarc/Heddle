using System;
using System.Collections.Generic;
using System.Linq;
using Templates.Data;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templates.Runtime {
    internal class RuntimeDocument : IDataProcessor {
        private readonly string _document;
        private readonly DataProcessor[] _optimizedElements;
        private readonly string[] _sourcePieces;
        private readonly IDataProcessor _singleProcessor;
        private bool _canDoFullOptimize;
        private readonly CompileScope _context;

        public RuntimeDocument(string document, DocumentElement[] executeItems, CompileScope context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            _context = context;
            _document = document;
            var optimizedElements = OptimizeCallTree(executeItems);
            if (optimizedElements.Count == 1)
            {
                _singleProcessor = optimizedElements.First();
            }
            _sourcePieces = GetDocumentPieces(optimizedElements, out _optimizedElements);
        }

        private struct DataProcessor
        {
            public IDataProcessor Processor;
            public bool NeedPieceWrite;
        }

        private ICollection<IDataProcessor> OptimizeCallTree(DocumentElement[] items)
        {
            if (items == null || items.Length == 0)
                return new IDataProcessor[0];
            List<IDataProcessor> resultTree = new List<IDataProcessor>();
            if (items.Length == 1 && items[0].CallChain.Count == 1)
            {
                var singleProcessor = items[0].CallChain.ItemsToExecute[0];
                singleProcessor.Position = items[0].Position;
                if (_document.Length == items[0].Position.Length &&
                    items[0].CallChain.ItemsToExecute[0].ReturnType == typeof(string))
                    _canDoFullOptimize = true;
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
            if (totalLength == _document.Length)
                _canDoFullOptimize = true;
            return resultTree;
        }

        private string[] GetDocumentPieces(ICollection<IDataProcessor> processors, out DataProcessor[] optimizedElements)
        {
            List<DataProcessor> optimized = new List<DataProcessor>();
            int offset = 0;
            List<string> pieces = new List<string>(processors.Count + 1);
            foreach (var element in processors)
            {
                if (element.Position.StartIndex > offset)
                {
                    pieces.Add(_document.Substring(offset, element.Position.StartIndex - offset));
                    optimized.Add(new DataProcessor
                    {
                        NeedPieceWrite = true,
                        Processor = element
                    });
                }
                else
                {
                    optimized.Add(new DataProcessor
                    {
                        NeedPieceWrite = false,
                        Processor = element
                    });
                }
                offset = element.Position.StartIndex + element.Position.Length;
            }
            if (_document.Length > offset)
            {
                pieces.Add(_document.Substring(offset));
            }
            optimizedElements = optimized.ToArray();
            return pieces.ToArray();
        }

        public object ProcessData(ref Scope data)
        {
            int count = _optimizedElements.Length;
            if (count > 0)
            {
                if (_canDoFullOptimize)
                {
                    var results = new string[_optimizedElements.Length];
                    var totalLength = 0;
                    for (int index = 0; index < _optimizedElements.Length; index++)
                    {
                        var result = _optimizedElements[index].Processor.ProcessData(ref data) as string ?? string.Empty;
                        totalLength += result.Length;

                        results[index] = result;
                    }
                    return ExStringBuilder.ConcatArray(results, totalLength);
                }
                // ReSharper disable once RedundantIfElseBlock
                else
                {
                    var results = new string[_optimizedElements.Length + _sourcePieces.Length];
                    var finalIndex = 0;
                    var pieceIndex = 0;
                    var totalLength = 0;
                    foreach (var element in _optimizedElements)
                    {
                        if (element.NeedPieceWrite)
                        {
                            results[finalIndex] = _sourcePieces[pieceIndex];
                            totalLength += _sourcePieces[pieceIndex].Length;

                            pieceIndex++;
                            finalIndex++;
                        }

                        var result = element.Processor.ProcessData(ref data) as string ?? string.Empty;
                        totalLength += result.Length;

                        results[finalIndex] = result;
                        finalIndex++;
                    }
                    if (finalIndex < results.Length)
                    {
                        results[finalIndex] = _sourcePieces[pieceIndex];
                        totalLength += _sourcePieces[pieceIndex].Length;
                    }
                    return ExStringBuilder.ConcatArray(results, totalLength);
                }
            }
            return _document;
        }

        public BlockPosition Position { get; set; }

        public bool Empty => _optimizedElements.Length == 0;

        public bool CanOptimizeSelf => _singleProcessor != null && _canDoFullOptimize;

        public IDataProcessor SingleProcessor => _singleProcessor;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var processor in _optimizedElements)
                {
                    processor.Processor.Dispose();
                }
                _context.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        public string Document => _document;

        public void Dispose()
        {
            Dispose(true);
        }

        ~RuntimeDocument()
        {
            Dispose(false);
        }

    }
}
