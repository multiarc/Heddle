using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Attributes;
using Templates.Core;

namespace Templates.Extensions {
    [Name("out")]
    public class OutExtension : AbstractHtmlExtension {
        protected override object ProcessDataInternal(object value, object chainedResult)
        {
            return chainedResult;
        }
    }
}