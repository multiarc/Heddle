using System.IO;

namespace Heddle.Precompiled
{
    /// <summary>Implemented by the generated artifact class a HeddleCompiledTemplatesAttribute names. Opens the
    /// embedded compiled form; the loader owns and disposes the stream. Stateless; thread-safe.</summary>
    public interface IHeddleCompiledArtifact
    {
        /// <summary>Opens the embedded compiled-form bytes for reading. Each call returns a new readable
        /// stream positioned at the start; the caller owns and disposes it.</summary>
        Stream OpenArtifact();
    }
}
