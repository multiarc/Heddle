using System;
using System.Collections.Generic;
using Templates.Data;
using Templates.Language;

namespace Templates.Runtime {
    internal static class DocumentsCache {
        private static readonly Dictionary<DocumentCacheItem, RuntimeDocument> Cache = new Dictionary<DocumentCacheItem, RuntimeDocument>();

        public static RuntimeDocument GetRuntimeDocument(string document, CompileContext context)
        {
            if (string.IsNullOrEmpty(document))
                return null;
            var itemToSearch = new DocumentCacheItem(document)  
            {
                RootPath = context.Options.RootPath,
                ModelType = context.ModelType
            };

            lock (Cache)
            {
                RuntimeDocument result;
                if (Cache.TryGetValue(itemToSearch, out result))
                    return result;
            }
            return null;
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

        internal static void DeleteMe(RuntimeDocument runtimeDocument, string document, CompileContext context) {
            var itemToSearch = new DocumentCacheItem(document)
            {
                RootPath = context.Options.RootPath,
                ModelType = context.ModelType
            };
            lock (Cache) {
                if (Cache.ContainsKey(itemToSearch)) {
                    Cache.Remove(itemToSearch);
                }
            }
        }

        public static void UpdateCaches(RuntimeDocument newRuntimeDocument, string oldDocument, CompileContext context)
        {
            if (!string.IsNullOrEmpty(oldDocument))
            {
                var itemToSearch = new DocumentCacheItem(oldDocument)
                {
                    RootPath = context.Options.RootPath,
                    ModelType = context.ModelType
                };
                lock (Cache)
                {
                    if (Cache.ContainsKey(itemToSearch))
                    {
                        Cache[itemToSearch].Dispose();
                        Cache.Remove(itemToSearch);
                    }
                    itemToSearch = new DocumentCacheItem(newRuntimeDocument.Document)
                    {
                        RootPath = context.Options.RootPath,
                        ModelType = context.ModelType
                    };
                    Cache.Add(itemToSearch, newRuntimeDocument);
                }
            }
            else
            {
                var itemToSearch = new DocumentCacheItem(newRuntimeDocument.Document)
                {
                    RootPath = context.Options.RootPath,
                    ModelType = context.ModelType
                };
                lock (Cache)
                {
                    if (Cache.ContainsKey(itemToSearch))
                    {
                        Cache[itemToSearch].Dispose();
                        Cache.Remove(itemToSearch);
                    }
                    Cache.Add(itemToSearch, newRuntimeDocument);
                }
            }
        }
    }
}