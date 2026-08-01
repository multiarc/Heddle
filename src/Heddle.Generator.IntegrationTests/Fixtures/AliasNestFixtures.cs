namespace Heddle.Generator.IntegrationTests.Fixtures.AliasNestAlpha
{
    /// <summary>Half of a pair of hosts declaring one nested name, so the nested name alone answers to two types
    /// and the short-name index therefore answers to neither. Without the collision a uniquely-named nested type
    /// resolves off the index with no directive at all, and a probe of what a directive contributes would pass
    /// whether the directive was read or not.</summary>
    public sealed class AliasHost
    {
        public sealed class AliasNested
        {
            public string Tag { get; set; }
        }
    }
}

namespace Heddle.Generator.IntegrationTests.Fixtures.AliasNestBeta
{
    public sealed class AliasHost
    {
        public sealed class AliasNested
        {
            public string Tag { get; set; }
        }
    }
}
