using Templates.Collections;
using Templates.Data;

namespace Templates.Language
{
    public class CallParameter
    {
        /// <summary>
        /// Use directly the ModelParameter or ChainParameter
        /// </summary>
        public string ModelParameter { get; set; }

        /// <summary>
        /// Use directly the ModelParameter or ChainParameter
        /// </summary>
        public SmartList<OutputItem> ChainParameter { get; set; }

        public bool IsModelTypeParameter => ChainParameter == null;

        public string CSharpExpression { get; set; }
    }
}