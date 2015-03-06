using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Extensions;

namespace Templates.Attributes {
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class ExportExtensionsAttribute: Attribute {
        private readonly Type[] _extensions;
        public bool All { get; }

        public IReadOnlyCollection<Type> Extensions => _extensions;

        public ExportExtensionsAttribute()
        {
            All = true;
        }

        public ExportExtensionsAttribute(Type extension)
        {
            _extensions = new[] {extension};
            All = false;
        }

        public ExportExtensionsAttribute(params Type[] extensions)
        {
            _extensions = extensions;
            All = false;
        }
    }
}
