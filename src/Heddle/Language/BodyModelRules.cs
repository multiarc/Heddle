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
        DeclaredOrParent
    }

    /// <summary>What a nested body sees on the chained channel.</summary>
    internal enum ChainedModelSource
    {
        /// <summary>Nothing host-specific; the ambient chained value.</summary>
        None,

        /// <summary>The boxed <see cref="int"/> iteration index (<c>@for</c>).</summary>
        Int32Index
    }

    /// <summary>
    /// <para>The body model-typing table. Which model a nested body is
    /// typed by is one of the highest-blast-radius rules in the emitter: a mistyped body changes which member,
    /// overload and conversion the emitted C# binds, and therefore the rendered value. Until this file the rule
    /// existed <em>only</em> as prose comments on each emission branch, guarded by an "is this the engine
    /// assembly" check.</para>
    /// <para>It stays a table rather than a shared resolver on purpose: the two resolvers derive the actual type
    /// from different worlds (Roslyn symbols vs. reflected <c>Type</c>s), and the drift surface here is the
    /// <em>choice</em>, not the type math. Conformance is enforced from both sides — a runtime test asserts each
    /// built-in's observed typing matches its row, a generator-side test asserts each pinned emission branch
    /// declares the same row.</para>
    /// </summary>
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
                ["list"] = (BodyModelSource.ElementOfData, ChainedModelSource.None)
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
