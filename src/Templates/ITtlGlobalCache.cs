using System;

namespace Templates
{
    public interface ITtlGlobalCache
    {
        void RemoveFromCache(int idMaster, int idContent);
        ITtlTemplate GetOrCreateTemplate(string masterTemplate, string template, DateTime dateUpdated, DateTime masterDateUpdated, int idMaster, int idContent);
    }
}