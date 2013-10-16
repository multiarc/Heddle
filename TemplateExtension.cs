namespace Templates {
    public static class TemplateExtension {
        public static string ProcessData (this TTLTemplate ttlTemplate, object data)
        {
            return ttlTemplate == null ? string.Empty : ttlTemplate.GenerateString(data);
        }
    }
}