#if DNXCORE50 || DNX451
using Microsoft.Framework.Runtime;
#endif
using System;
using System.Collections.Generic;
using Templates.Exceptions;

namespace Templates
{
    public class TtlGlobalCache : ITtlGlobalCache
    {
        private readonly Dictionary<TemplateCacheItem, ITtlTemplate> _cache = new Dictionary<TemplateCacheItem, ITtlTemplate>();
        private static readonly object LockObject = new object();

        private struct TemplateCacheItem: IEquatable<TemplateCacheItem> {
            private readonly string _master;
            private readonly string _template;
            private readonly DateTime _dateUpdated;

            public TemplateCacheItem(string master, string template, DateTime dateUpdated) {
                _template = template;
                _master = master;
                _dateUpdated = dateUpdated;
            }

            public bool Equals(TemplateCacheItem other) {
                return other._master == _master && other._template == _template && other._dateUpdated == _dateUpdated;
            }

            public override int GetHashCode() {
                return _dateUpdated.GetHashCode();
            }

            public override bool Equals(object obj) {
                if (!(obj is TemplateCacheItem))
                    return false;
                return Equals((TemplateCacheItem)obj);
            }

            public static bool operator ==(TemplateCacheItem one, TemplateCacheItem another) {
                return one.Equals(another);
            }

            public static bool operator !=(TemplateCacheItem one, TemplateCacheItem another) {
                return !(one == another);
            }
        }

        public void RemoveFromCache(string masterTemplate, string template, DateTime dateUpdated) {
            var searchValue = new TemplateCacheItem(masterTemplate, template, dateUpdated);
            lock (LockObject) {
                if (_cache.ContainsKey(searchValue)) {
                    var ttlTemplate = _cache[searchValue];
                    _cache.Remove(searchValue);
                    ttlTemplate.Dispose();
                }
            }
        }

        public ITtlTemplate GetOrCreateTemplate(string masterTemplate, string template, DateTime dateUpdated) {
            ITtlTemplate result;
            var searchValue = new TemplateCacheItem(masterTemplate, template, dateUpdated);
            lock(LockObject) {
                if (_cache.TryGetValue(searchValue, out result)) {
                    return result;
                }
            }
            result = new TtlTemplate(masterTemplate + template);
            if (!result.CompileResult.Success)
                throw new TemplateCompileException(result.CompileResult.Errors);
            lock (LockObject) {
                _cache.Add(searchValue, result);
            }
            return result;
        }
    }
}