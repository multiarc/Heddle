using System.Collections.Generic;
using Templates.Data;

namespace Templates.Runtime
{
    public interface ITemplateResolver
    {
        TtlTemplate GetTemplate(string viewName, string controllerName, out IEnumerable<string> searchedLocations, CompileContext context = null, TemplatePathType searchType = TemplatePathType.None);

        string Search(string viewName, string controllerName, TemplatePathType searchType, out IEnumerable<string> searchedLocations, out TtlTemplate cached);

        TtlTemplate Create(string viewName, CompileContext context);
        void RemoveFromCache(TtlTemplate template);
    }
}