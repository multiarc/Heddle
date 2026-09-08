namespace Heddle.Precompiled
{
    /// <summary>Implemented beside <see cref="IHeddleCompiledArtifact"/> by a generated artifact class
    /// that carries generated sites. Stateless after type init; thread-safe.</summary>
    public interface IPrecompiledSiteTable
    {
        /// <summary>The artifact digest (AC-6) of the artifact this table was printed from.</summary>
        string ArtifactDigest { get; }

        /// <summary>Returns the generated delegate for one site id. Creation happens once per process;
        /// the loader uses the delegate when its type matches the site kind's shape.</summary>
        bool TryGetSite(string templateContentHash, int templateIndex, int siteOrdinal, out System.Delegate site);
    }
}
