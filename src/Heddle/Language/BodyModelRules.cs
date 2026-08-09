using System;
using System.Collections.Generic;

namespace Heddle.Language
{
    /// <summary>Where a nested body's <c>ModelData</c> comes from.</summary>
    internal enum BodyModelSource
    {
        /// <summary>The enclosing body's model — the host re-scopes with <c>scope.Parent()</c>.</summary>
        Parent,

        /// <summary>The host's own positional data value.</summary>
        Data,

        /// <summary>The element type of the host's <c>IEnumerable&lt;T&gt;</c> data; dynamic when the sequence is
        /// non-generic or itself dynamic.</summary>
        ElementOfData,

        /// <summary>The declared <c>:: T</c> of a definition.</summary>
        Declared,

        /// <summary>The declared slot type when the definition declares one, else the positional data type
        /// (the runtime's <c>callerModelType = slotType ?? dataType</c>).</summary>
        SlotOrData,

        /// <summary>A region body: its declared <c>:: T</c>, or — for a bare call on an untyped region — the
        /// enclosing model.</summary>
        DeclaredOrParent,

        /// <summary>The value on the chained channel — the host hands its own <c>chainedType</c> to the body compile
        /// (<c>@out</c>, <c>@swap</c>). The name the table never had, because the table only ever pinned the seven
        /// body-hosting names the emitter emits itself; the hook probe answers for every extension, and two of the
        /// built-ins answer this.</summary>
        Chained
    }

    /// <summary>What a nested body sees on the chained channel.</summary>
    internal enum ChainedModelSource
    {
        /// <summary>Nothing host-specific; the ambient chained value.</summary>
        None,

        /// <summary>The boxed <see cref="int"/> iteration index (<c>@for</c>, <c>@list</c>).</summary>
        Int32Index,

        /// <summary>The enclosing body's model, put on the chained channel (<c>@out</c>: it swaps the two channels
        /// so the body sees the chained value as its model and the model as its chained).</summary>
        Parent,

        /// <summary>The host's own positional data value, put on the chained channel (<c>@swap</c>).</summary>
        Data
    }

    /// <summary>The body model-typing table: which channel a body-hosting extension's body is compiled against.
    /// <para><b>It is no longer a prediction.</b> Every row is held equal to what the extension's own
    /// <c>InitStart</c> does by <c>HookProbeLockstepTests</c>, which probes every registered extension and compares.
    /// That is what makes the table safe to consult when nothing can be probed — a build that has not opted into
    /// hook probing, or one whose engine reference sits somewhere no assembly may be loaded from — and it is what
    /// caught the <c>@list</c> chained column being wrong.</para>
    /// <para>Rows are added by observation, not by guess: the way to add one is to run the probe, read the roles it
    /// reports, and let the lockstep suite hold them there.</para></summary>
    internal static class BodyModelRules
    {
        /// <summary>The typing of a definition body: its declared <c>:: T</c>.</summary>
        internal const BodyModelSource DefinitionBody = BodyModelSource.Declared;

        /// <summary>The typing of an invocation site's caller content: the slot type when the callee declares
        /// one, else the positional data type.</summary>
        internal const BodyModelSource CallerContent = BodyModelSource.SlotOrData;

        /// <summary>The typing of a region body (default or materialized fill).</summary>
        internal const BodyModelSource RegionBody = BodyModelSource.DeclaredOrParent;

        private static readonly Dictionary<string, (BodyModelSource Body, ChainedModelSource Chained)> Table =
            new Dictionary<string, (BodyModelSource, ChainedModelSource)>(StringComparer.Ordinal)
            {
                ["if"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["ifnot"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["elif"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["elseif"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["else"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["for"] = (BodyModelSource.Parent, ChainedModelSource.Int32Index),
                // Int32Index, not None: ListExtension.InitStart hands `new ExType(typeof(int))` to the body compile
                // and ProcessData/RenderData call scope.Model(item, index), so an @out() in a @list body splices the
                // iteration index exactly as it does in a @for body. The row read None for as long as it existed;
                // it was latent because the emitter consults the body column alone, and it is read now.
                ["list"] = (BodyModelSource.ElementOfData, ChainedModelSource.Int32Index),
                // The step-back encoders. Nine extensions, one hook body between them —
                // `base.InitStart(ctx, parent, chainedType, null)` — which re-types the DEFAULT BODY against the
                // caller's scope and changes nothing else. The build tier knew four of them and refused the other
                // five for no reason but the list's length; the probe reports the same role for all nine.
                ["string"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["attr"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["url"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["js"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["int"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["money"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["date"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["time"] = (BodyModelSource.Parent, ChainedModelSource.None),
                ["guid"] = (BodyModelSource.Parent, ChainedModelSource.None)
            };

        /// <summary>The row for a body-hosting built-in extension name, or <c>false</c> when the name declares no
        /// pinned typing (a custom extension's body typing is its own <c>InitStart</c>'s business, which is
        /// exactly why the emitter refuses to precompile a bodied custom call).</summary>
        internal static bool TryGet(string extensionName, out BodyModelSource body, out ChainedModelSource chained)
        {
            if (extensionName != null && Table.TryGetValue(extensionName, out var row))
            {
                body = row.Body;
                chained = row.Chained;
                return true;
            }

            body = BodyModelSource.Data;
            chained = ChainedModelSource.None;
            return false;
        }

        /// <summary>Every pinned built-in name, for the conformance suites on both sides.</summary>
        internal static IEnumerable<string> PinnedNames => Table.Keys;
    }
}
