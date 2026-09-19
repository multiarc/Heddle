using System;
using System.Collections.Generic;
using System.IO;

namespace Heddle.Tests
{
    /// <summary>
    /// Probe assemblies written beside the test binaries, removed when the process ends.
    /// <para>They have to be written somewhere findable: a probe the runtime is asked <i>not</i> to load proves
    /// nothing if it could not have been loaded, and one loaded by path must have a real <c>Location</c> for the
    /// observation and reference-building paths to treat it the way they treat a host's own assemblies. Every name
    /// carries a fresh GUID so "this has never been loaded" is a fact rather than a hope about test ordering — which
    /// left the output directory one <c>.dll</c> richer per probe per run, permanently.</para>
    /// <para>Deletion waits for process exit rather than the end of the test that wrote it, because by then the file
    /// is loaded and other suites read its <c>Location</c>: the C# tier builds its reference set from the locations
    /// of every observed assembly, and removing one mid-run leaves that set short of a reference and an unrelated
    /// test red. Best effort — a platform that locks a mapped image simply keeps the file.</para>
    /// </summary>
    internal static class ProbeAssemblyFiles
    {
        private static readonly List<string> Written = new List<string>();
        private static bool _hooked;

        internal static string WriteBesideTestAssembly(byte[] bytes, string assemblyName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
            File.WriteAllBytes(path, bytes);
            lock (Written)
            {
                if (!_hooked)
                {
                    AppDomain.CurrentDomain.ProcessExit += (_, __) => DeleteAll();
                    _hooked = true;
                }

                Written.Add(path);
            }

            return path;
        }

        private static void DeleteAll()
        {
            lock (Written)
            {
                foreach (var path in Written)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                Written.Clear();
            }
        }
    }
}
