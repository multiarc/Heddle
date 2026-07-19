using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Runtime
{
    /// <summary>
    /// One recorded body-compile span (phase 6 D2): the effective model and chained types the compiler
    /// threaded for the body whose text starts at <see cref="Offset"/> (absolute UTF-16 code units) and runs
    /// <see cref="Length"/> units. Definition bodies compile once per call site, so one body span can carry
    /// several entries — one per site's effective type; the artificial-type rule (D13) is a query over them.
    /// </summary>
    internal readonly struct ScopeMapEntry
    {
        public ScopeMapEntry(int offset, int length, ExType modelType, ExType chainedType)
        {
            Offset = offset;
            Length = length;
            ModelType = modelType;
            ChainedType = chainedType;
        }

        public int Offset { get; }

        public int Length { get; }

        public ExType ModelType { get; }

        public ExType ChainedType { get; }
    }

    /// <summary>
    /// <para>Append-only retention of the per-body-span model/chained types the compiler computes (phase 6 D2).
    /// Created on <see cref="CompileContext"/> only when <see cref="Data.TemplateOptions.ProvideLanguageFeatures"/>
    /// is true and reference-copied through the private copy ctor so every child compile shares one map — the
    /// map is engine-accurate by construction because every body funnels through
    /// <c>HeddleCompiler.Compile</c>.</para>
    /// <para>Compile-time state on a single-threaded compile; never read at render time.</para>
    /// </summary>
    internal sealed class ScopeMap
    {
        private readonly List<ScopeMapEntry> _entries = new List<ScopeMapEntry>();

        /// <summary>The root (document) model type, recorded once at the first body compile.</summary>
        public ExType RootType { get; private set; }

        /// <summary>All recorded entries, in record (document-compile) order.</summary>
        public IReadOnlyList<ScopeMapEntry> Entries => _entries;

        /// <summary>Records one body-compile span with its effective types (the D2 single recording site).</summary>
        public void Record(int offset, int length, ExType modelType, ExType chainedType)
        {
            RootType ??= modelType;
            _entries.Add(new ScopeMapEntry(offset, length, modelType, chainedType));
        }
    }
}
