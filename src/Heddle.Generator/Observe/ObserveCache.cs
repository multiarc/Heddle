using System;
using System.IO;

namespace Heddle.Generator.Observe
{
    /// <summary>
    /// The content-addressed file store engine observation loads from, and the whole safety argument for loading
    /// anything at all out of a build directory.
    /// <para><see cref="System.Reflection.Assembly.LoadFrom(string)"/> holds a file open for the life of the
    /// process, and a compiler is a persistent server — which is why loading a consumer's <c>bin</c>/<c>obj</c>
    /// output was previously refused outright. Every file this store writes is named after a digest of its own
    /// content, so a changed input is a <b>different file</b> and the permanent lock is always on a file nothing
    /// will ever rewrite. Immutability is a property of the naming scheme rather than of a path allow-list.</para>
    /// <para><b>Completeness is explicit.</b> A writer creates <c>&lt;name&gt;</c> exclusively and only then writes
    /// the zero-byte <c>&lt;name&gt;.ok</c> marker beside it, and a reader accepts a file only once the marker
    /// opens. Two compilations racing on the same digest therefore never hand each other a half-written image: the
    /// loser reads no marker, observes nothing this round, and finds a complete file the next.</para>
    /// <para>RS1035 shapes the API surface here. <c>System.IO.File</c> and <c>System.IO.Directory</c> are banned in
    /// an analyzer and <c>System.IO.FileStream</c> is not, so nothing below asks whether a file exists — it opens
    /// one and reads the answer off the exception — and the directory itself is created by MSBuild
    /// (<c>_HeddleCreateObserveCache</c>) rather than here.</para>
    /// </summary>
    internal static class ObserveCache
    {
        /// <summary>The marker written beside a completed file; its presence is what makes the file readable.</summary>
        private const string CompletionMarker = ".ok";

        /// <summary>Whether <paramref name="path"/> names a file this store finished writing. Never throws.</summary>
        internal static bool IsComplete(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                using (new FileStream(path + CompletionMarker, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures a content-addressed file exists at <paramref name="path"/>, writing it through
        /// <paramref name="write"/> only when it is not already complete.
        /// </summary>
        /// <returns><c>true</c> when the file is complete and readable — whether this call wrote it or found it.
        /// <c>false</c> is always "observation is unavailable this round", never an error.</returns>
        /// <param name="wrote">Set when this call actually produced the file, so a purity test can count emissions
        /// rather than infer them.</param>
        internal static bool Ensure(string path, Action<Stream> write, out bool wrote)
        {
            wrote = false;
            if (IsComplete(path))
                return true;

            try
            {
                // FileShare.None while writing: a racing reader gets a sharing violation rather than a truncated
                // image, and the marker below is never written for a write that faulted.
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    write(stream);
            }
            catch (Exception)
            {
                // Somebody else created it (CreateNew races), or this build may not write here at all. Either way
                // the only question left is whether a complete file is there now.
                return IsComplete(path);
            }

            try
            {
                using (new FileStream(path + CompletionMarker, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                }
            }
            catch (Exception)
            {
                return false;
            }

            wrote = true;
            return true;
        }

        /// <summary>Copies <paramref name="source"/> to a path named after its own content digest, and returns that
        /// path. The copy is what gets loaded, so the lock never lands on the file the build produced.</summary>
        internal static string CopyContentAddressed(string source, string directory, string simpleName,
            string contentTag, out bool wrote)
        {
            wrote = false;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(directory))
                return null;

            var path = Path.Combine(directory, simpleName + "." + contentTag + ".dll");
            bool copied;
            var ok = Ensure(path, stream =>
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        stream.Write(buffer, 0, read);
                }
            }, out copied);

            wrote = copied;
            return ok ? path : null;
        }
    }
}
