namespace Templates.Core {
    internal class DefenitionBaseExtension : AbstractExtension {
        public override object ProcessData(object value, object chainedResult)
        {
            return GetInnerResult(value, chainedResult);
        }
    }
}