namespace Templates {
    public static class TemplateExtension {
        public static string ProcessData (this TtlTemplate ttlTemplate, object data)
        {
            return ttlTemplate == null ? string.Empty : ttlTemplate.Generate(data);
        }
    }
}