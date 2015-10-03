namespace Templates.Core {
    internal class DefenitionBaseExtension : AbstractExtension {
        public DefenitionBaseExtension DefenitionTemplate { get; set; }

        public override object ProcessData(object data, object chained)
        {
            chained = GetInnerResult(data, chained);
            return DefenitionTemplate?.ProcessData(data, chained) ?? chained;
        }
    }
}