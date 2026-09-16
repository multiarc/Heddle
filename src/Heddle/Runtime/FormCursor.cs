using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Strings.Core;

namespace Heddle.Runtime
{
    /// <summary>The loader's answer to a body compile a hook requests while materializing a compiled form.
    /// It serves the item's recorded body (raw text) and checks the hook's live consumed types against the
    /// recorded ones; a hook that hands a differing data or chained type faults the item with
    /// <see cref="PrecompiledMismatchException"/> carrying
    /// <see cref="PrecompiledFallbackReason.ExtensionInitTypingMismatch"/>, which the per-item compile catch
    /// turns into a compile error and phase 1's binding gate later classifies.
    /// <para>Correlation is by document and item position: the loader parses the recorded raw text, so a
    /// served body re-parses to the positions the build recorded. Nested bodies stack by the served body's
    /// <c>CompiledDocumentRef</c>; a <c>null</c> ref (or <c>-1</c>) is a leaf and pushes an empty map.</para>
    /// <para>The cursor rides on <see cref="CompileScope.FormCursor"/> for bodies; materialization also arms
    /// it as the thread ambient so <c>HeddleTemplate.Compile(CompileContext)</c> — which only receives a
    /// context — can serve a named child from the same artifact. Hooks run unchanged in both cases.</para></summary>
    internal sealed class FormCursor
    {
        [ThreadStatic]
        private static FormCursor _current;

        /// <summary>The cursor armed by the in-flight materialization on this thread, if any.</summary>
        internal static FormCursor Current => _current;

        private readonly struct BodyKey : IEquatable<BodyKey>
        {
            internal readonly int Start;
            internal readonly int Length;
            internal readonly string Template;

            internal BodyKey(int start, int length, string template)
            {
                Start = start;
                Length = length;
                Template = template ?? string.Empty;
            }

            public bool Equals(BodyKey other) =>
                Start == other.Start && Length == other.Length &&
                string.Equals(Template, other.Template, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is BodyKey other && Equals(other);

            public override int GetHashCode() =>
                ((Start * 397) ^ Length) * 397 ^ Template.GetHashCode();
        }

        private sealed class ServedBody
        {
            internal string RawText;
            internal CompiledTypeRef DataType;
            internal CompiledTypeRef ChainedType;
            internal int DocumentRef;
        }

        private readonly string _templateKey;
        private readonly List<Dictionary<BodyKey, ServedBody>> _documents =
            new List<Dictionary<BodyKey, ServedBody>>();
        private readonly Dictionary<string, int> _namedChildren =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Dictionary<BodyKey, string>> _refusals =
            new List<Dictionary<BodyKey, string>>();

        /// <summary>One open document frame. <c>Bypass</c> marks a refusal-fragment compile: the
        /// fragment owns its recompile, so bodies serve nothing and never throw — embedded bodies
        /// compile from live text exactly as on a dynamic compile (the recorded bodies carry the
        /// build's typings, possibly deferred, which a bound recompile must not inherit). A bypass
        /// frame also records the refusal key it is serving, so the fragment's own top item cannot
        /// re-serve the same site into unbounded recursion; nested independent refusals still serve.
        /// A null parameter template normalizes to empty: both mean "no template".</summary>
        private struct Frame
        {
            internal int DocumentRef;
            internal bool Bypass;
            internal bool ServesRefusal;
            internal int ServedStart;
            internal int ServedLength;
            internal string ServedTemplate;
        }

        private readonly Stack<Frame> _open = new Stack<Frame>();
        private readonly Dictionary<BodyKey, ServedBody> _empty =
            new Dictionary<BodyKey, ServedBody>();
        private IList<CompiledDocument> _shaped = new List<CompiledDocument>();

        private FormCursor(string templateKey)
        {
            _templateKey = templateKey ?? string.Empty;
        }

        /// <summary>Builds the cursor over a decoded artifact. No resolution happens here: recorded types
        /// are compared nominally, so nothing is loaded.</summary>
        internal static FormCursor OverArtifact(CompiledArtifact artifact, string templateKey)
        {
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            var cursor = new FormCursor(templateKey);
            if (artifact.Documents == null)
                throw new ArgumentNullException(nameof(artifact));
            cursor._shaped = artifact.Documents;
            for (int d = 0; d < artifact.Documents.Count; d++)
            {
                var map = new Dictionary<BodyKey, ServedBody>();
                var refusals = new Dictionary<BodyKey, string>();
                var document = artifact.Documents[d];
                if (document != null)
                {
                    if (document.Elements != null)
                    {
                        foreach (var element in document.Elements)
                        {
                            if (element == null || !element.IsChain || element.Chain == null ||
                                element.Chain.Items == null)
                                continue;
                            foreach (var item in element.Chain.Items)
                                AddItem(item, map, refusals);
                        }
                    }
                    // Removed-element orphans: items the build compiled but cut from the elements.
                    // Their bodies were requested all the same, so they serve exactly like kept ones.
                    if (document.RemovedItems != null)
                        foreach (var removed in document.RemovedItems)
                            AddItem(removed, map, refusals);
                }

                cursor._documents.Add(map);
                cursor._refusals.Add(refusals);
            }

            if (artifact.Templates != null)
            {
                foreach (var row in artifact.Templates)
                {
                    if (row == null || string.IsNullOrEmpty(row.RegisteredName))
                        continue;
                    if (!cursor._namedChildren.ContainsKey(row.RegisteredName))
                        cursor._namedChildren.Add(row.RegisteredName, row.RootDocumentRef);
                }
            }

            return cursor;
        }

        /// <summary>Adds one recorded item's bodies (and refusals) to a document map. The primary body
        /// serves under the item's template; each alternate body serves under its own requested template
        /// (a definition's default body compiles under the definition's template, its caller content
        /// under the call's). Every body is also reachable under its own raw text. First writer wins on
        /// collision, as before.</summary>
        private static void AddItem(CompiledItem item, Dictionary<BodyKey, ServedBody> map,
            Dictionary<BodyKey, string> refusals)
        {
            if (item == null || item.Position == null)
                return;
            var key = new BodyKey(item.Position.Start, item.Position.Length,
                item.ParameterTemplate ?? string.Empty);
            if (item.Body != null)
                Serve(map, item.Position, item.ParameterTemplate, item.Body);
            if (item.AltBodies != null)
                foreach (var alt in item.AltBodies)
                    if (alt != null && alt.Body != null)
                        Serve(map, item.Position, alt.Template, alt.Body);

            if (item.Parameter != null &&
                item.Parameter.Kind == CompiledParameterKind.RefusalSite &&
                item.Parameter.Refusal != null &&
                item.Parameter.Refusal.SourceText != null &&
                !refusals.ContainsKey(key))
                refusals.Add(key, item.Parameter.Refusal.SourceText);
        }

        private static void Serve(Dictionary<BodyKey, ServedBody> map, CompiledPosition position,
            string template, CompiledBody body)
        {
            var served = new ServedBody
            {
                RawText = body.RawText ?? string.Empty,
                DataType = body.DataType,
                ChainedType = body.ChainedType,
                DocumentRef = body.CompiledDocumentRef ?? -1
            };
            var key = new BodyKey(position.Start, position.Length, template ?? string.Empty);
            if (!map.ContainsKey(key))
                map.Add(key, served);
            var alias = new BodyKey(position.Start, position.Length, served.RawText);
            if (!alias.Equals(key) && !map.ContainsKey(alias))
                map.Add(alias, served);
        }

        /// <summary>Opens an unaffiliated file fallback: a child the artifact does not carry, compiled
        /// live from disk exactly as the build compiled it. Bodies serve live and refusals never match,
        /// so the outer document's recorded keys cannot collide with the file's positions. Balanced by
        /// <see cref="ExitBody"/>.</summary>
        internal void EnterUnaffiliated()
        {
            _open.Push(new Frame { DocumentRef = -1, Bypass = true });
        }

        /// <summary>Opens the root document. The loader calls this once before compiling the root text.</summary>
        internal void EnterRoot(int documentRef)
        {
            _open.Clear();
            Push(documentRef);
        }

        private void Push(int documentRef)
        {
            _open.Push(new Frame { DocumentRef = documentRef });
        }

        private Frame TopFrame()
        {
            if (_open.Count == 0)
                return new Frame { DocumentRef = -1 };
            return _open.Peek();
        }

        private Dictionary<BodyKey, ServedBody> Top()
        {
            var frame = TopFrame();
            if (frame.Bypass || frame.DocumentRef < 0 || frame.DocumentRef >= _documents.Count)
                return _empty;
            return _documents[frame.DocumentRef];
        }

        /// <summary>Serves the recorded body for the item at <paramref name="position"/>, pushing its
        /// nested map. A miss while armed is a host fault (<see cref="InvalidOperationException"/>): the
        /// artifact must cover every body the materialization requests — silently compiling the hook's
        /// text would hide an artifact that is not self-sufficient. The per-item compile catch turns the
        /// throw into a positioned compile error, so nothing renders.</summary>
        internal bool TryServeBody(BlockPosition position, string parameterTemplate, ExType dataType,
            ExType chainedType, out string rawText)
        {
            rawText = null;
            var frame = TopFrame();
            // A refusal-fragment frame serves nothing and never throws: the out value stays the
            // hook's live text, which the caller then compiles as a fresh recompile.
            if (frame.Bypass)
            {
                rawText = parameterTemplate;
                return false;
            }
            ServedBody served;
            if (!Top().TryGetValue(new BodyKey(position.StartIndex, position.Length,
                parameterTemplate ?? string.Empty), out served))
                throw new InvalidOperationException("Cannot materialize '" + _templateKey +
                    "': the artifact carries no recorded body for the item at " +
                    position.StartIndex + ":" + position.Length + " ('" + parameterTemplate +
                    "'); the artifact does not cover this template.");
            CheckConsumedTypes(position, served, dataType, chainedType);
            Push(served.DocumentRef);
            rawText = served.RawText;
            return true;
        }

        /// <summary>Pops the map pushed by <see cref="TryServeBody"/> or
        /// <see cref="TryEnterNamedChild"/> after the served compile finishes.</summary>
        internal void ExitBody()
        {
            if (_open.Count != 0)
                _open.Pop();
        }

        /// <summary>Serves the refusal source recorded for the item at <paramref name="position"/> in the
        /// open document, if the build refused it. Returns false for ordinary items, for items under a
        /// fragment frame whose coordinates cannot match (fragment-local positions never equal the
        /// recorded outer keys — nested items compile normally), and for a site an enclosing fragment
        /// frame is already serving (its own top item re-matching would recurse without bound).</summary>
        internal bool TryGetRefusal(BlockPosition position, string parameterTemplate, out string sourceText)
        {
            sourceText = null;
            var frame = TopFrame();
            if (frame.DocumentRef < 0 || frame.DocumentRef >= _refusals.Count)
                return false;
            string template = parameterTemplate ?? string.Empty;
            // A site an enclosing fragment frame is already serving stays dark: the fragment's own
            // top item re-matching its site (a zero-offset slice re-parses to identical coordinates)
            // would otherwise recurse without bound, as would mutually-referential sites.
            foreach (var open in _open)
                if (open.ServesRefusal && open.ServedStart == position.StartIndex &&
                    open.ServedLength == position.Length &&
                    string.Equals(open.ServedTemplate, template, StringComparison.Ordinal))
                    return false;
            return _refusals[frame.DocumentRef].TryGetValue(
                new BodyKey(position.StartIndex, position.Length, template), out sourceText);
        }

        /// <summary>Opens the frame a refusal-fragment compile runs under. The frame always bypasses
        /// body serving (the fragment owns its recompile); the return is the source-text offset the
        /// fragment maps to, or -1 when <paramref name="sourceText"/> is not a true slice. A rebuilt
        /// source restores the call's leading <c>@</c> (item spans exclude it), so the item's own start
        /// is tried second after the <c>@</c> position. Balanced by <see cref="ExitBody"/>.</summary>
        internal int EnterRefusalFragment(BlockPosition itemPosition, string parameterTemplate,
            string sourceText)
        {
            var frame = TopFrame();
            int offset = -1;
            if (!frame.Bypass && frame.DocumentRef >= 0 && frame.DocumentRef < _shaped.Count &&
                _shaped[frame.DocumentRef] != null && sourceText != null)
                offset = TrueSliceOffset(SourceText(_shaped[frame.DocumentRef]),
                    itemPosition.StartIndex, sourceText);
            _open.Push(new Frame
            {
                DocumentRef = frame.DocumentRef,
                Bypass = true,
                ServesRefusal = true,
                ServedStart = itemPosition.StartIndex,
                ServedLength = itemPosition.Length,
                ServedTemplate = parameterTemplate ?? string.Empty
            });
            return offset;
        }

        private static int TrueSliceOffset(string shaped, int start, string sourceText)
        {
            if (shaped != null && IsTrueSlice(shaped, start, sourceText))
                return start;
            if (start > 0 && shaped != null && start - 1 + sourceText.Length <= shaped.Length &&
                shaped[start - 1] == '@' && IsTrueSlice(shaped, start - 1, sourceText))
                return start - 1;
            return -1;
        }

        private static bool IsTrueSlice(string shaped, int offset, string sourceText)
        {
            if (shaped == null || offset < 0 || sourceText.Length == 0 ||
                offset + sourceText.Length > shaped.Length)
                return false;
            for (int i = 0; i < sourceText.Length; i++)
                if (shaped[offset + i] != sourceText[i])
                    return false;
            return true;
        }

        /// <summary>Serves the root text of the sibling row registered under <paramref name="name"/>,
        /// pushing its document map. Returns false when the artifact carries no such row.</summary>
        internal bool TryEnterNamedChild(string name, out string shapedText)
        {
            shapedText = null;
            if (string.IsNullOrEmpty(name))
                return false;
            int documentRef;
            if (!_namedChildren.TryGetValue(name, out documentRef))
                return false;
            if (_shaped == null || documentRef < 0 || documentRef >= _shaped.Count ||
                _shaped[documentRef] == null)
                return false;
            Push(documentRef);
            shapedText = SourceText(_shaped[documentRef]);
            return true;
        }

        /// <summary>The text the loader parses for a recorded document: the pre-shaping source when
        /// the build recorded one, else the shaped text (synthesized fragment documents, which the
        /// loader never parses, and artifacts predating raw recording).</summary>
        internal static string SourceText(CompiledDocument document)
        {
            if (document == null)
                return string.Empty;
            return !string.IsNullOrEmpty(document.RawText) ? document.RawText :
                document.ShapedText ?? string.Empty;
        }

        private void CheckConsumedTypes(BlockPosition position, ServedBody served, ExType dataType,
            ExType chainedType)
        {
            string dataDetail = MismatchDetail("data", served.DataType, dataType);
            if (dataDetail != null)
                throw Fault(position, dataDetail);
            string chainedDetail = MismatchDetail("chained", served.ChainedType, chainedType);
            if (chainedDetail != null)
                throw Fault(position, chainedDetail);
        }

        private static string MismatchDetail(string side, CompiledTypeRef recorded, ExType live)
        {
            if (recorded == null)
                return null;
            string recordedNominal = recorded.Nominal();
            string liveNominal = FormRecord.ToTypeRef(live)?.Nominal();
            if (string.Equals(recordedNominal, liveNominal, StringComparison.Ordinal))
                return null;
            // AC-4: a framework type compares by full name alone, so a body typed over System.Object
            // recorded on .NET 10 (System.Private.CoreLib) is the same type on net48 (mscorlib).
            var liveType = live != null ? live.Type : null;
            if (liveType != null && !(recorded is DynamicTypeRef) &&
                Heddle.Precompiled.PrecompiledGauntlet.TypeRefMatches(recorded, liveType))
                return null;
            return "the " + side + " type (" + (liveNominal ?? "<untyped>") +
                ") differs from the recorded consumed type (" + recordedNominal + ")";
        }

        private PrecompiledMismatchException Fault(BlockPosition position, string detail) =>
            new PrecompiledMismatchException(_templateKey,
                PrecompiledFallbackReason.ExtensionInitTypingMismatch,
                "a body at " + position.StartIndex + ":" + position.Length +
                " compiled against " + detail);

        /// <summary>Arms the thread ambient for the duration of a materialization. Nested arms stack by
        /// previous value; the caller disposes to restore.</summary>
        internal IDisposable ArmAmbient()
        {
            var previous = _current;
            _current = this;
            return new AmbientRestore(previous);
        }

        private sealed class AmbientRestore : IDisposable
        {
            private readonly FormCursor _previous;
            private bool _disposed;

            internal AmbientRestore(FormCursor previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _current = _previous;
                }
            }
        }
    }
}
