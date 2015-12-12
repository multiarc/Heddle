using Templates.Collections;
using Templates.Strings.Core;

namespace Templates.Language
{
    public class CallParameter
    {
        /// <summary>
        /// Use directly the ModelParameter or ChainParameter
        /// </summary>
        public string[] ModelParameter { get; set; }

        public bool RootReference { get; set; }

        /// <summary>
        /// Use directly the ModelParameter or ChainParameter
        /// </summary>
        public SmartList<OutputItem> ChainParameter { get; set; }

        public bool IsModelTypeParameter => ChainParameter == null && CSharpExpression == null;

        public string CSharpExpression { get; set; }
    }
}