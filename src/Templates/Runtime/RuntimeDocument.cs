using System;
using System.Globalization;
using System.Threading;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templates.Runtime {
    public class RuntimeDocument : IDisposable
    {
        private readonly string _document;
        private readonly DocumentElement[] _executeItems;
        private readonly ThreadLocal<Replacement[]> _threadLocal;
        private readonly Type _modelType;

        internal RuntimeDocument(string document, DocumentElement[] executeItems = null, Type modelType = null)
        {
            _document = document;
            _executeItems = executeItems;
            _threadLocal = new ThreadLocal<Replacement[]>(MakeNewArray);
            _modelType = modelType ?? typeof(object);
        }

        public string ProcessData(object data, object chainedResult)
        {
            if (_executeItems.Length == 0)
            {
                return _document;
            }
#if DEBUG
            if (data != null && !_modelType.IsType(data)) {
                throw new TemplateProcessingException
                    (string.Format
                         (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}", _modelType.FullName,
                          data.GetType().FullName));
            }
#endif
            Replacement[] replacements = _threadLocal.Value;
            for (int i = 0; i < _executeItems.Length; i++)
            {
                var result = _executeItems[i].CallChain.ProcessData(data, chainedResult);
                if (!(result is string))
                    replacements[i].ReplacementValue = result?.ToString();
                else
                    replacements[i].ReplacementValue = result as string;
            }
            return ExStringBuilder.BulkReplace(replacements, _document);
        }

        public bool Empty => _executeItems.Length == 0;

        private Replacement[] MakeNewArray() {
            var replacements = new Replacement[_executeItems.Length];
            for (int i = 0; i < _executeItems.Length; i++)
                replacements[i].BlockPosition = _executeItems[i].BlockPosition;
            return replacements;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
            _threadLocal.Dispose();
        }

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
