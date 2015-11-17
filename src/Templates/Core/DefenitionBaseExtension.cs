namespace Templates.Core {
    internal class DefenitionBaseExtension : AbstractExtension {
        public DefenitionBaseExtension DefenitionTemplate { get; set; }

        public override object ProcessData(object data, object chained, object parent)
        {
            chained = GetInnerResult(data, chained);
            return DefenitionTemplate?.ProcessData(data, chained, parent) ?? chained;
        }
    }
}