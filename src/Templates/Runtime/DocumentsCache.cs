using System;
using System.Collections.Generic;
using System.Linq;
using Templates.Data;
using Templates.Helpers;


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

            DocumentParser result;
            lock (Cache) {
                if (Cache.TryGetValue(itemToSearch, out result))
                    return result;
                result = new DocumentParser(context);
                Cache.Add(itemToSearch, result);
            }
            result.Parse(document);
            return result;
        }

        public static void Clear()
        {
            lock (Cache)
            {
                foreach (var value in Cache.Values)
                {
                    value.Dispose();
                }
                Cache.Clear();
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