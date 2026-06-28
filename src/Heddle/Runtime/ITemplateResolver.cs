using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Runtime
{
    public interface ITemplateResolver
    {
        HeddleTemplate GetTemplate(string viewName, string controllerName, out IEnumerable<string> searchedLocations, CompileContext context = null, TemplatePathType searchType = TemplatePathType.None);

        string Search(string viewName, string controllerName, TemplatePathType searchType, out IEnumerable<string> searchedLocations, out HeddleTemplate cached);

        HeddleTemplate Create(string viewName, CompileContext context);
        void RemoveFromCache(HeddleTemplate template);
    }
}