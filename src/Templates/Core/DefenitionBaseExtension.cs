namespace Templates.Core {
    internal class DefenitionBaseExtension : AbstractHtmlExtension {
        protected override object ProcessDataInternal(object value, object chainedResult)
        {
            return GetInnerResult(value, chainedResult);
        }
    }
}