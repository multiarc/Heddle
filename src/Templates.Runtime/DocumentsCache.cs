using System;
using System.Collections.Generic;
using Templates.Core.Data;

namespace Templates.Runtime {
    public static class DocumentsCache {
        private static readonly Dictionary<DocumentCacheItem, DocumentParser> Cache = new Dictionary<DocumentCacheItem, DocumentParser>();

        public static DocumentParser GetDocumentParser (string document, CompileContext context)
        {
            if (string.IsNullOrEmpty(document))
                return null;
            var itemToSearch = new DocumentCacheItem(document)
            {
                RootPath = context.Options.RootPath,
                ModelType = context.ModelType
            };

            lock (Cache) {
                DocumentParser result;
                if (Cache.TryGetValue(itemToSearch, out result))
                    return result;
                result = new DocumentParser(document, context);
                Cache.Add(itemToSearch, result);
                return result;
            }
        }

        public static void UpdateCaches (DocumentParser newParser, string document, Type modelType, string rootPath)
        {
            if (!string.IsNullOrEmpty(document)) {
                var itemToSearch = new DocumentCacheItem(document)
                {
                    RootPath = rootPath,
                    ModelType = modelType
                };
                lock (Cache) {
                    if (Cache.ContainsKey(itemToSearch))
                        Cache.Remove(itemToSearch);
                    itemToSearch = new DocumentCacheItem(newParser.Document)
                    {
                        RootPath = rootPath,
                        ModelType = modelType
                    };
                    Cache.Add(itemToSearch, newParser);
                }
            }
        }
    }
}