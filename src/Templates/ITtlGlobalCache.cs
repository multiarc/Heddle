using System;

namespace Templates
{
    public interface ITtlGlobalCache
    {
        void RemoveFromCache(string masterTemplate, string template, DateTime dateUpdated);
        ITtlTemplate GetOrCreateTemplate(string masterTemplate, string template, DateTime dateUpdated);
    }
}