using System;

namespace Templates
{
    public interface ITtlGlobalCache
    {
        void RemoveFromCache(string masterTemplate, string template, DateTime dateUpdated, DateTime masterDateUpdated);
        ITtlTemplate GetOrCreateTemplate(string masterTemplate, string template, DateTime dateUpdated, DateTime masterDateUpdated);
    }
}