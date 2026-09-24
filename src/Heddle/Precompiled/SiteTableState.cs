using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Precompiled.CompiledForm;

namespace Heddle.Precompiled
{
    /// <summary>Per-materialization generated-site state. The loader builds one around the row's site
    /// records, eagerly resolves every record the artifact's table serves (failing fast on a digest or
    /// shape mismatch — both host faults), and hands delegates to the compile points, which match their
    /// own record by value (member chain, expression position, C# source and position). Identical twins
    /// are interchangeable, so first-unconsumed matching is exact; anything unmatched rebuilds from data.
    /// A null table (or a disabled preference switch) means the data path: nothing is consulted
    /// and, for accessor/native/C# sites, strict mode does not throw — there is no site id to name.
    /// Refusal and late-bound records throw under strict with or without a table (see MaterializeNow and
    /// the deferred hook): they are row data, not table lookups.</summary>
    internal sealed class SiteTableState
    {
        public readonly IPrecompiledSiteTable Table;
        public readonly string Key;
        public readonly string ContentHash;
        public readonly int TemplateIndex;
        public readonly bool Strict;
        public readonly bool Active;

        private readonly IList<CompiledSiteRow> _records;
        private readonly CompiledArtifact _artifact;
        private readonly Delegate[] _delegates;
        private readonly bool[] _consumed;

        private SiteTableState(IPrecompiledSiteTable table, string key, string contentHash, int templateIndex,
            bool strict, IList<CompiledSiteRow> records, CompiledArtifact artifact, bool active,
            Delegate[] delegates)
        {
            Table = table;
            Key = key;
            ContentHash = contentHash;
            TemplateIndex = templateIndex;
            Strict = strict;
            Active = active;
            _records = records;
            _artifact = artifact;
            _delegates = delegates;
            _consumed = new bool[records.Count];
        }

        public static SiteTableState Create(IPrecompiledSiteTable table, bool useTable, string key,
            string contentHash, int templateIndex, bool strict, IList<CompiledSiteRow> records,
            CompiledArtifact artifact)
        {
            if (records == null)
                records = Array.Empty<CompiledSiteRow>();
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            // Only this template's rows, grouped once per artifact by the loader: the table key is the
            // site id (content hash, template index, ordinal), so asking with this template's id for
            // another template's row addresses a different site — whose delegate carries the wrong
            // shape. The loader rebuilds those from data instead; twin matching stays within the
            // template, where the build de-duplicated them.
            if (table == null || !useTable)
                return new SiteTableState(null, key, contentHash, templateIndex, strict, records, artifact,
                    false, new Delegate[records.Count]);
            if (!string.Equals(table.ArtifactDigest ?? string.Empty, artifact.DigestHex ?? string.Empty,
                StringComparison.Ordinal))
                throw new InvalidOperationException("Template '" + key + "' ships a generated site table " +
                    "for artifact digest '" + table.ArtifactDigest + "' but the artifact carries '" +
                    artifact.DigestHex + "'; rebuild the templates with the generated source.");
            var delegates = new Delegate[records.Count];
            var state = new SiteTableState(table, key, contentHash, templateIndex, strict, records, artifact,
                true, delegates);
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record == null)
                    continue;
                if (record.Kind == CompiledSiteKind.LateBoundCall || record.Kind == CompiledSiteKind.Refusal)
                    continue;
                Delegate site;
                if (!table.TryGetSite(contentHash, templateIndex, record.SiteOrdinal, out site) || site == null)
                    continue;
                delegates[i] = CheckedShape(state, record, site);
            }

            return state;
        }

        public static int FindNativeRecord(SiteTableState state, int positionStart, int positionLength)
        {
            if (state == null)
                return -1;
            for (int i = 0; i < state._records.Count; i++)
            {
                if (state._consumed[i])
                    continue;
                var record = state._records[i];
                if (record == null || record.Kind != CompiledSiteKind.NativeExpression)
                    continue;
                var trees = state._artifact.Expressions;
                if (trees == null || record.PayloadRef < 0 || record.PayloadRef >= trees.Count)
                    continue;
                var tree = trees[record.PayloadRef];
                if (tree == null || tree.Root == null || tree.Root.Position == null)
                    continue;
                if (tree.Root.Position.Start == positionStart && tree.Root.Position.Length == positionLength)
                    return i;
            }

            return -1;
        }

        public static bool NativeRecordDeferred(SiteTableState state, int found)
        {
            if (state == null || found < 0 || found >= state._records.Count)
                return false;
            var record = state._records[found];
            if (record == null)
                return false;
            var trees = state._artifact.Expressions;
            if (trees == null || record.PayloadRef < 0 || record.PayloadRef >= trees.Count)
                return false;
            var tree = trees[record.PayloadRef];
            return tree != null && tree.ContainsDeferredCall;
        }

        public static int NativeRecordOrdinal(SiteTableState state, int found)
        {
            if (state == null || found < 0 || found >= state._records.Count || state._records[found] == null)
                return -1;
            return state._records[found].SiteOrdinal;
        }

        private static Delegate CheckedShape(SiteTableState state, CompiledSiteRow record, Delegate site)
        {
            Type expected = ExpectedShape(record.Kind);
            if (expected != null && !expected.IsInstanceOfType(site))
                throw new InvalidOperationException("Template '" + state.Key + "' site ordinal " +
                    record.SiteOrdinal + " (" + record.Kind + ") has delegate type '" +
                    site.GetType().FullName + "'; expected '" + expected.FullName + "'.");
            return site;
        }

        internal static Type ExpectedShape(CompiledSiteKind kind)
        {
            switch (kind)
            {
                case CompiledSiteKind.MemberAccessor:
                    return typeof(Func<object, object>);
                case CompiledSiteKind.NativeExpression:
                    return null;
                case CompiledSiteKind.EmbeddedCSharp:
                    return typeof(Func<object, object, object, object>);
                default:
                    return null;
            }
        }

        private int FindMember(string[] segments, bool rootRef, ExType startType,
            List<(Type Declaring, string Name, Type Member)> hops)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_consumed[i])
                    continue;
                var record = _records[i];
                if (record == null || record.Kind != CompiledSiteKind.MemberAccessor)
                    continue;
                if (MemberMatches(record, segments, rootRef, startType, hops))
                    return i;
            }

            return -1;
        }

        private bool MemberMatches(CompiledSiteRow record, string[] segments, bool rootRef, ExType startType,
            List<(Type Declaring, string Name, Type Member)> hops)
        {
            var members = _artifact.Members;
            if (members == null || record.PayloadRef < 0 || record.PayloadRef >= members.Count)
                return false;
            var member = members[record.PayloadRef];
            if (member == null || member.Segments == null)
                return false;
            if (member.Segments.Count != (segments != null ? segments.Length : 0))
                return false;
            for (int i = 0; i < member.Segments.Count; i++)
                if (!string.Equals(member.Segments[i] ?? string.Empty, segments[i] ?? string.Empty,
                    StringComparison.Ordinal))
                    return false;
            var recordedHops = member.Hops;
            int hopCount = recordedHops != null ? recordedHops.Count : 0;
            int liveCount = hops != null ? hops.Count : 0;
            if (hopCount != liveCount)
                return false;
            for (int i = 0; i < hopCount; i++)
            {
                var recorded = recordedHops[i];
                var live = hops[i];
                if (recorded == null)
                    return false;
                if (!string.Equals(recorded.MemberName ?? string.Empty, live.Item2 ?? string.Empty,
                    StringComparison.Ordinal))
                    return false;
                if (!NominalEquals(recorded.DeclaringType, live.Item1) ||
                    !NominalEquals(recorded.MemberType, live.Item3))
                    return false;
            }

            return NominalEquals(member.StartType, startType != null ? startType.Type : null);
        }

        internal static bool NominalEquals(CompiledTypeRef recorded, Type live)
        {
            if (recorded == null)
                return live == null;
            if (live == null)
                return false;
            // Framework refs match by full name alone; everything else by full name and assembly.
            return PrecompiledGauntlet.TypeRefMatches(recorded, live);
        }

        internal static string NominalOf(Type live)
        {
            if (live == null)
                return string.Empty;
            string fullName = live.FullName ?? live.ToString();
            string assembly = live.Assembly != null ? live.Assembly.GetName().Name : string.Empty;
            return fullName + ", " + assembly;
        }

        public static bool TryResolveAccessor(SiteTableState state, string[] segments, bool rootRef,
            ExType startType, List<(Type Declaring, string Name, Type Member)> hops, out Delegate site,
            out int ordinal, out string kind)
        {
            site = null;
            ordinal = -1;
            kind = "MemberAccessor";
            if (state == null)
                return false;
            return state.TryTake(state.FindMember(segments, rootRef, startType, hops), kind,
                typeof(Func<object, object>), out site, out ordinal, out kind);
        }

        public static bool TryResolveNative(SiteTableState state, int positionStart, int positionLength,
            Type expectedShape, out Delegate site, out int ordinal, out string kind)
        {
            site = null;
            ordinal = -1;
            kind = "NativeExpression";
            if (state == null)
                return false;
            int found = FindNativeRecord(state, positionStart, positionLength);
            return state.TryTake(found, kind, expectedShape, out site, out ordinal, out kind);
        }

        public static bool TryResolveCSharp(SiteTableState state, string source, int positionStart,
            int positionLength, out Delegate site, out int ordinal, out string kind)
        {
            site = null;
            ordinal = -1;
            kind = "CSharp";
            if (state == null)
                return false;
            int found = -1;
            for (int i = 0; i < state._records.Count; i++)
            {
                if (state._consumed[i])
                    continue;
                var record = state._records[i];
                if (record == null || record.Kind != CompiledSiteKind.EmbeddedCSharp)
                    continue;
                var sites = state._artifact.CSharpSites;
                if (sites == null || record.PayloadRef < 0 || record.PayloadRef >= sites.Count)
                    continue;
                var csharp = sites[record.PayloadRef];
                if (csharp == null)
                    continue;
                if (!string.Equals(csharp.Source ?? string.Empty, source ?? string.Empty,
                    StringComparison.Ordinal))
                    continue;
                if (csharp.Position == null || csharp.Position.Start != positionStart ||
                    csharp.Position.Length != positionLength)
                    continue;
                found = i;
                break;
            }

            return state.TryTake(found, kind, typeof(Func<object, object, object, object>), out site,
                out ordinal, out kind);
        }

        private bool TryTake(int found, string kind, Type expectedShape, out Delegate site, out int ordinal,
            out string kindOut)
        {
            if (!Active || found < 0)
            {
                site = null;
                ordinal = -1;
                kindOut = kind;
                return false;
            }

            _consumed[found] = true;
            site = _delegates[found];
            ordinal = _records[found].SiteOrdinal;
            kindOut = kind;
            if (site == null)
                return false;
            if (expectedShape != null && !expectedShape.IsInstanceOfType(site))
                throw new InvalidOperationException("Template '" + Key + "' site ordinal " + ordinal +
                    " (" + kind + ") has delegate type '" + site.GetType().FullName + "'; expected '" +
                    expectedShape.FullName + "'.");
            return true;
        }

        public void ThrowStrict(string kind, int ordinal)
        {
            throw new PrecompiledStrictLoadException(Key, ordinal, kind);
        }

        /// <summary>Throws the strict exception for a record-backed site the table left unserved. Only
        /// when the table is active (present and preferred) and the record gave an ordinal: without a
        /// table there is no site id to name, and unmatched text rebuilds from data.</summary>
        public void ThrowIfStrictUnserved(string kind, int ordinal)
        {
            // Under strict load with a table present, a site the table did not serve is a refusal whether it
            // matched a record (ordinal >= 0) or no record exists for it at all (ordinal -1): both would
            // compile at load, which is what strict load forbids.
            if (Strict && Active)
                throw new PrecompiledStrictLoadException(Key, ordinal, kind);
        }
    }
}
