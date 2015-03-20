namespace Templates.Core {
    internal class DefenitionBaseExtension : AbstractExtension {
        public DefenitionBaseExtension DefenitionTemplate { get; set; }

        public override object ProcessData(object value, object chainedResult)
        {
            chainedResult = GetInnerResult(value, chainedResult);
            return DefenitionTemplate?.ProcessData(value, chainedResult) ?? chainedResult;
        }
    }
}