using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templates.Runtime {
    internal class RuntimeDocument : IDataProcessor {
        private readonly string _document;
        private readonly IDataProcessor[] _optimizedElements;
        private readonly BlockPosition[] _outputPositions;
        private readonly IDataProcessor _singleProcessor;
        private bool _canDoFullOptimize;
        private readonly CompileContext _context;

        public RuntimeDocument(string document, DocumentElement[] executeItems, CompileContext context)
        {
            _context = context;
            _document = document;
            _optimizedElements = OptimizeCallTree(executeItems);
            if (_optimizedElements.Length == 1)
            {
                _singleProcessor = _optimizedElements[0];
            }
            _outputPositions = new BlockPosition[_optimizedElements.Length];
            for (int i = 0; i < _optimizedElements.Length; i++)
            {
                _outputPositions[i] = _optimizedElements[i].Position;
            }
        }

        private IDataProcessor[] OptimizeCallTree(DocumentElement[] items)
        {
            if (items == null || items.Length == 0)
                return new IDataProcessor[0];
            List<IDataProcessor> resultTree = new List<IDataProcessor>();
            if (items.Length == 1 && items[0].CallChain.Count == 1)
            {
                var singleProcessor = items[0].CallChain.ItemsToExecute[0];
                singleProcessor.Position = items[0].Position;
                if (_document.Length == items[0].Position.Length &&
                    items[0].CallChain.ItemsToExecute[0].ReturnType == typeof (string))
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
            return resultTree.ToArray();
        }


        public object ProcessData(object data, object chainedResult)
        {
            if (_singleProcessor != null)
            {
                string replacementValue = _singleProcessor.ProcessData(data, chainedResult) as string ?? string.Empty;
                if (_canDoFullOptimize)
                    return replacementValue;
                return ExStringBuilder.Replace(_singleProcessor.Position.StartIndex, _singleProcessor.Position.Length,
                    replacementValue, _document);
            }
            if (_canDoFullOptimize) {
                ExStringBuilder builder = new ExStringBuilder();
                foreach (IDataProcessor item in _optimizedElements) {
                    builder.Append(item.ProcessData(data, chainedResult) as string);
                }
                return builder.ToString();
            }
            unsafe
            {
                int count = _optimizedElements.Length;
                if (count > 0)
                {
                    var values = stackalloc char*[count];
                    var lengths = stackalloc int[count];
                    var handles = stackalloc GCHandle[count];
                    for (int i = 0; i < count; i++)
                    {
                        var resultBlock = _optimizedElements[i].ProcessData(data, chainedResult) as string;
                        if (resultBlock != null)
                        {
                            lengths[i] = resultBlock.Length;
                            var handle = GCHandle.Alloc(resultBlock, GCHandleType.Pinned);
                            handles[i] = handle;
                            values[i] = (char*) handle.AddrOfPinnedObject().ToPointer();
                        }
                        else
                        {
                            lengths[i] = 0;
                            values[i] = null;
                        }
                    }
                    var result = ExStringBuilder.BulkReplace(values, lengths, _outputPositions, _document);
                    for (int i = 0; i < count; i++)
                    {
                        if (handles[i].IsAllocated)
                            handles[i].Free();
                    }
                    return result;
                }
                return _document;
            }
        }

        public BlockPosition Position { get; set; }

        public bool Empty => _optimizedElements.Length == 0;

        public bool CanOptimizeSelf => _singleProcessor != null && _canDoFullOptimize;

        public IDataProcessor SingleProcessor => _singleProcessor;

        //private Replacement[] MakeNewArray() {
        //    var replacements = new Replacement[_optimizedElements.Length];
        //    for (int i = 0; i < _optimizedElements.Length; i++)
        //        replacements[i].BlockPosition = _optimizedElements[i].Position;
        //    return replacements;
        //}

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var processor in _optimizedElements)
                {
                    processor.Dispose();
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
