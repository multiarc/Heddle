namespace Heddle.Precompiled
{
    /// <summary>One entry of a precompiled template's transitive <c>@&lt;&lt;</c> import closure (phase 7 D6): the
    /// import's normalized key and the SHA-256 (lowercase hex) of its raw bytes. Editing an imported file changes
    /// its hash and so invalidates the importing template's precompiled copy under <c>EnableFileChangeCheck</c>.</summary>
    public readonly struct PrecompiledImport
    {
        public PrecompiledImport(string key, string contentHash)
        {
            Key = key;
            ContentHash = contentHash;
        }

        public string Key { get; }

        public string ContentHash { get; }
    }
}
