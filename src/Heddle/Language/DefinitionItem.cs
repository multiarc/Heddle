using System;
using System.Collections.Generic;
using Heddle.Strings.Core;

namespace Heddle.Language {
    /// <summary>
    /// Definition syntax element with inheritance support
    /// </summary>
    public class DefinitionItem {
        internal DefinitionItem(DefinitionItem definition) : this(definition, long.MaxValue, ParseContext.CurrentIsolationStamp)
        {
        }

        /// <summary>A copy of <paramref name="definition"/> as the whole of it stood at <paramref name="asOf"/>:
        /// its position and body, and every link of its base chain. A base is often the very item another
        /// definition is registered under, which may have been overridden in place since — and an override
        /// keeps the state it replaces as a copy of its own, made later still.</summary>
        internal DefinitionItem(DefinitionItem definition, long asOf) : this(definition, asOf, asOf)
        {
        }

        private DefinitionItem(DefinitionItem definition, long asOf, long copiedAt)
        {
            var baseDefinition = definition.BaseAsOf(asOf);
            if (baseDefinition != null)
            {
                BaseDefinition = new DefinitionItem(baseDefinition, asOf, copiedAt)
                {
                    _copiedFrom = definition.BaseDefinition
                };
            }

            // A copy an isolation reads late still stands where the isolation does: what changes it afterwards is
            // a change since then, to whatever is isolated from it in turn.
            _copiedAt = copiedAt;
            _stateStamp = copiedAt;
            if (asOf == long.MaxValue && definition._states != null)
            {
                _states = new List<State>(definition._states);
                _stateStamp = definition._stateStamp;
            }

            definition.StateAsOf(asOf, out _position, out _context, out _deferredContext);
            ModelType = definition.ModelType;
            Name = definition.Name;
            ParameterTemplate = definition.ParameterTemplate;
            HasDefaultOutput = definition.HasDefaultOutput;
            PropDeclarations = definition.PropDeclarations;
            SlotTypeName = definition.SlotTypeName;
            IsRegion = definition.IsRegion;
            IsPublicRegion = definition.IsPublicRegion;
            Regions = definition.Regions;
            IsFillCandidate = definition.IsFillCandidate;
        }

        public DefinitionItem(string name, string parameterTemplate, DefinitionItem baseDefinition, string modelType = null)
        {
            FullOverride = name == baseDefinition?.Name;
            Name = name;
            ParameterTemplate = parameterTemplate;
            BaseDefinition = baseDefinition;
            ModelType = modelType?.Trim() ?? "object";
            _stateStamp = ParseContext.CurrentIsolationStamp;
            _context = new ParseContext();
        }

        public void OverrideWith(DefinitionItem item)
        {
            long stamp = ParseContext.NextIsolationStamp();
            (_overriddenAt ?? (_overriddenAt = new List<long>())).Add(stamp);
            // What this item was is kept whole in the copy, so the fields are written without remembering them.
            BaseDefinition = new DefinitionItem(this, long.MaxValue, stamp);
            _stateStamp = stamp;
            _position = item.Position;
            _context = item.Context;
            _deferredContext = null;
            Name = item.Name;
            ParameterTemplate = item.ParameterTemplate;
            ModelType = item.ModelType;
            PropDeclarations = item.PropDeclarations;
            SlotTypeName = item.SlotTypeName;
        }

        /// <summary>
        /// Declared props of this declaration layer, inheritance not flattened (base props live on
        /// <see cref="BaseDefinition"/>). Empty for a header without a prop list. Set by the parser.
        /// </summary>
        public IReadOnlyList<PropDeclaration> PropDeclarations { get; internal set; } = Array.Empty<PropDeclaration>();

        /// <summary>
        /// The declared slot parameter type name (<c>out:: Type</c>), or <c>null</c> when the definition does
        /// not parameterize its slot. Set by the parser.
        /// </summary>
        public string SlotTypeName { get; internal set; }

        /// <summary>
        /// True when this declaration layer carried a default output (<c>-&gt; chain</c>). Preserved across
        /// full overrides — the default chain declared by an earlier layer keeps rendering at document end, so
        /// the double-render warning (HED4002) stays accurate. Set by the parser; read-only for hosts.
        /// </summary>
        public bool HasDefaultOutput { get; internal set; }

        /// <summary>
        /// True for a definition declared inside a component body — a named content region
        /// (public via <c>&lt;:name&gt;</c> or a private inner <c>&lt;name&gt;</c>). Never true for a
        /// document-scope definition. Copied by the copy ctor; deliberately NOT part of
        /// <see cref="OverrideWith"/>'s field-copy list (the sibling idiom must not clobber region-ness).
        /// </summary>
        internal bool IsRegion { get; set; }

        /// <summary>The <c>&lt;:name&gt;</c> public-region form. Meaningful only when
        /// <see cref="IsRegion"/> is true.</summary>
        internal bool IsPublicRegion { get; set; }

        /// <summary>
        /// The directly-declared regions of this component's body (declaration order), appended on
        /// the <c>EnterDef</c> store-success path only, so a rejected duplicate never lands here. Empty for a
        /// region-less definition — the region table is additive metadata.
        /// </summary>
        internal System.Collections.Generic.IReadOnlyList<RegionDeclaration> Regions { get; set; } =
            System.Array.Empty<RegionDeclaration>();

        /// <summary>
        /// True for a call-body <c>&lt;x:x&gt;</c> whose base is unresolved — a captured
        /// <see cref="RegionFillCandidate"/>. Such an item is never registered into any
        /// <see cref="DefinitionBlock"/> (it must not self-shadow the region default a self-call resolves to).
        /// </summary>
        internal bool IsFillCandidate { get; set; }

        public bool FullOverride { get; set; }
        public DefinitionItem BaseDefinition { get; private set; }

        public BlockPosition Position
        {
            get { return _position; }
            set
            {
                RememberState();
                _position = value;
            }
        }

        /// <summary>The definition's body context. On a definition that belongs to an isolated context it is
        /// produced on first read: isolating a context copies every definition visible in it, and copying every
        /// one of their bodies as well, for each output chain of a document, is what made parsing cost the number
        /// of calls times the square of the number of definitions.</summary>
        public ParseContext Context
        {
            get
            {
                var context = _context;
                if (context != null)
                    return context;
                var deferred = _deferredContext;
                if (deferred == null)
                    return _context;
                context = deferred.Materialize();
                lock (ParseContext.SharedLock)
                {
                    if (ReferenceEquals(_deferredContext, deferred))
                    {
                        _context = context;
                        _deferredContext = null;
                    }

                    return _context;
                }
            }
            set
            {
                lock (ParseContext.SharedLock)
                {
                    RememberState();
                    _context = value;
                    _deferredContext = null;
                }
            }
        }

        private struct State
        {
            internal long Until;
            internal BlockPosition Position;
            internal ParseContext Context;
            internal ParseContext.DeferredIsolation Deferred;
        }

        private BlockPosition _position;
        private ParseContext _context;
        private ParseContext.DeferredIsolation _deferredContext;
        private List<long> _overriddenAt;
        private List<State> _states;
        private long _stateStamp;
        private DefinitionItem _copiedFrom;
        private long _copiedAt;

        /// <summary>Called before the position or the body changes. An isolated context reads a definition when
        /// it is first asked for it, and has to find what the definition was when the context was isolated — so a
        /// value that an isolation may have seen is kept, with the last stamp that saw it. One that no isolation
        /// can have seen, because none was taken since it was written, is simply replaced.</summary>
        private void RememberState()
        {
            long now = ParseContext.CurrentIsolationStamp;
            if (now == _stateStamp)
                return;
            (_states ?? (_states = new List<State>())).Add(new State
            {
                Until = now, Position = _position, Context = _context, Deferred = _deferredContext
            });
            _stateStamp = now;
        }

        private void StateAsOf(long stamp, out BlockPosition position, out ParseContext context,
            out ParseContext.DeferredIsolation deferred)
        {
            lock (ParseContext.SharedLock)
            {
                if (_states != null)
                {
                    foreach (var state in _states)
                    {
                        if (state.Until < stamp)
                            continue;
                        position = state.Position;
                        context = state.Context;
                        deferred = state.Deferred;
                        return;
                    }
                }

                position = _position;
                context = _context;
                deferred = _deferredContext;
            }
        }

        /// <summary>This definition as it stood at <paramref name="stamp"/>. An override at the document's own
        /// level rewrites the item in place and keeps what it was as <see cref="BaseDefinition"/>, so the state
        /// before the overrides made since then is that many bases down.</summary>
        internal DefinitionItem AsOf(long stamp)
        {
            var item = this;
            if (_overriddenAt == null)
                return item;
            for (int i = _overriddenAt.Count - 1; i >= 0 && _overriddenAt[i] > stamp && item.BaseDefinition != null; i--)
                item = item.BaseDefinition;
            return item;
        }

        /// <summary>The base this definition had at <paramref name="stamp"/>, as that base stood then. A link
        /// copied after the stamp — an override copies the whole chain under the state it keeps — stands for the
        /// item it was copied from, which is where the chain led at the time.</summary>
        private DefinitionItem BaseAsOf(long stamp)
        {
            var link = BaseDefinition;
            while (link != null && link._copiedFrom != null && link._copiedAt > stamp)
                link = link._copiedFrom;
            return link?.AsOf(stamp);
        }

        /// <summary>Makes the body context an isolated copy, within <paramref name="tree"/>, of the one this item
        /// carries now — made when it is first read.</summary>
        internal void DeferContextIsolation(ParseContext.IsolationTree tree, long stamp)
        {
            if (_deferredContext != null)
            {
                _deferredContext = new ParseContext.DeferredIsolation(null, _deferredContext, stamp, tree);
                _context = null;
            }
            else if (_context != null)
            {
                _deferredContext = new ParseContext.DeferredIsolation(_context, null, stamp, tree);
                _context = null;
            }
        }

        public string Name { get; private set; }
        public string ParameterTemplate { get; private set; }
        public string ModelType { get; private set; }
    }
}