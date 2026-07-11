using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;

// Exports the custom branch-role trio (and the bodiless zebra-style participant) to the dynamic backend so a
// custom-trio template resolves the same [ExtensionName] types on both tiers — the WI6 (§8.3) universality gate.
[assembly: ExportExtensions(
    typeof(Heddle.Generator.IntegrationTests.Fixtures.YellExtension),
    typeof(Heddle.Generator.IntegrationTests.Fixtures.BeginExtension),
    typeof(Heddle.Generator.IntegrationTests.Fixtures.BetweenExtension),
    typeof(Heddle.Generator.IntegrationTests.Fixtures.FinishExtension),
    typeof(Heddle.Generator.IntegrationTests.Fixtures.FlagExtension),
    typeof(Heddle.Generator.IntegrationTests.Fixtures.GateExtension),
    typeof(Heddle.Generator.IntegrationTests.Fixtures.RowExtension))]

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>Shared, public-API-only branch semantics for the custom trio (mirrors the built-in
    /// <c>@if</c>/<c>@elif</c>/<c>@else</c> family through the public <see cref="Scope.Publish"/>/
    /// <see cref="Scope.TryRead"/> channel).</summary>
    internal static class BranchTrioSupport
    {
        public static bool Truthy(object value) => value != null && (!(value is bool b) || b);

        /// <summary>Opportunistic publish (R11): an opener carries no <c>[ScopeChannel]</c>, so a set with no
        /// participant sibling provisions no frame and <see cref="Scope.Publish"/> throws — swallowed, mirroring
        /// the built-in openers' frameless no-op.</summary>
        public static void TryPublish(in Scope scope, bool satisfied)
        {
            try { scope.Publish(BranchState.ReservedKey, new BranchState(satisfied)); }
            catch (InvalidOperationException) { }
        }

        public static bool ReadSatisfied(in Scope scope, out bool present)
        {
            if (scope.TryRead(BranchState.ReservedKey, out var value) && value is BranchState state)
            {
                present = true;
                return state.Satisfied;
            }

            present = false;
            return false;
        }
    }

    /// <summary>Opener — canonical shape with the <c>InitStart</c> parent-model override (R12). Because it overrides a
    /// compile-time hook, the emitter degrades a call to it to the dynamic tier (no HED7015, §6.3.3).</summary>
    [ExtensionName("begin")]
    [BranchRole(BranchRole.Opener)]
    public sealed class BeginExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
            => base.InitStart(initContext, parent, chainedType, null);

        public override object ProcessData(in Scope scope)
        {
            bool satisfied = BranchTrioSupport.Truthy(scope.ModelData);
            BranchTrioSupport.TryPublish(scope, satisfied);
            return satisfied ? GetInnerResult(scope.Parent()) : string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            bool satisfied = BranchTrioSupport.Truthy(scope.ModelData);
            BranchTrioSupport.TryPublish(scope, satisfied);
            if (satisfied) RenderInnerResult(scope.Parent());
        }
    }

    /// <summary>Continuation — elif-shaped (reads channel, republishes, may render). Overrides <c>InitStart</c>.</summary>
    [ExtensionName("between")]
    [ScopeChannel]
    [BranchRole(BranchRole.Continuation)]
    public sealed class BetweenExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
            => base.InitStart(initContext, parent, chainedType, null);

        public override object ProcessData(in Scope scope)
        {
            if (BranchTrioSupport.ReadSatisfied(scope, out _)) return string.Empty;
            bool truthy = BranchTrioSupport.Truthy(scope.ModelData);
            scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
            return truthy ? GetInnerResult(scope.Parent()) : string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            if (BranchTrioSupport.ReadSatisfied(scope, out _)) return;
            bool truthy = BranchTrioSupport.Truthy(scope.ModelData);
            scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
            if (truthy) RenderInnerResult(scope.Parent());
        }
    }

    /// <summary>Terminal — else-shaped (reads, renders when unsatisfied, throws when no set is open).</summary>
    [ExtensionName("finish")]
    [ScopeChannel]
    [BranchRole(BranchRole.Terminal)]
    public sealed class FinishExtension : AbstractExtension
    {
        internal const string NoOpenerMessage = "'@finish' is a branch terminal with no matching opener in this scope.";

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
            => base.InitStart(initContext, parent, chainedType, null);

        public override object ProcessData(in Scope scope)
        {
            bool satisfied = BranchTrioSupport.ReadSatisfied(scope, out var present);
            if (!present) throw new TemplateProcessingException(NoOpenerMessage);
            return satisfied ? string.Empty : GetInnerResult(scope.Parent());
        }

        public override void RenderData(in Scope scope)
        {
            bool satisfied = BranchTrioSupport.ReadSatisfied(scope, out var present);
            if (!present) throw new TemplateProcessingException(NoOpenerMessage);
            if (!satisfied) RenderInnerResult(scope.Parent());
        }
    }

    /// <summary>A bodiless-friendly Opener that overrides <b>no</b> compile-time hook — so the emitter binds a
    /// bodiless call to it directly on the custom path (§6.3.2). Publishes opportunistically and echoes its value.</summary>
    [ExtensionName("flag")]
    [BranchRole(BranchRole.Opener)]
    public sealed class FlagExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            BranchTrioSupport.TryPublish(scope, BranchTrioSupport.Truthy(scope.ModelData));
            return scope.ModelData?.ToString() ?? string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            BranchTrioSupport.TryPublish(scope, BranchTrioSupport.Truthy(scope.ModelData));
            scope.Renderer.Render(scope.ModelData?.ToString() ?? string.Empty);
        }
    }

    /// <summary>A bodiless-friendly Continuation ([ScopeChannel], no hook) — binds directly and provisions a frame.
    /// Used with <c>@flag</c> to prove the generator's role-based strip machine removes inter-block text on the
    /// precompiled tier exactly as the runtime does.</summary>
    [ExtensionName("gate")]
    [ScopeChannel]
    [BranchRole(BranchRole.Continuation)]
    public sealed class GateExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            if (BranchTrioSupport.ReadSatisfied(scope, out _)) return string.Empty;
            bool truthy = BranchTrioSupport.Truthy(scope.ModelData);
            scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
            return truthy ? (scope.ModelData?.ToString() ?? string.Empty) : string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            if (BranchTrioSupport.ReadSatisfied(scope, out _)) return;
            bool truthy = BranchTrioSupport.Truthy(scope.ModelData);
            scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
            if (truthy) scope.Renderer.Render(scope.ModelData?.ToString() ?? string.Empty);
        }
    }

    /// <summary>A roleless bodiless <c>[ScopeChannel]</c> participant (the documented "zebra" pattern): publishes to
    /// a private key each sibling flips. Binds directly on the precompiled tier; the §5.3.3a fix makes the hosting
    /// body provision a locals frame (keyed off <c>HasScopeChannel</c>) so <see cref="Scope.Publish"/> no longer
    /// throws at render.</summary>
    [ExtensionName("row")]
    [ScopeChannel]
    public sealed class RowExtension : AbstractExtension
    {
        private const string Key = "fixtures.row.odd";

        public override object ProcessData(in Scope scope) => Next(scope);

        public override void RenderData(in Scope scope) => scope.Renderer.Render(Next(scope));

        private static string Next(in Scope scope)
        {
            bool odd = scope.TryRead(Key, out var v) && v is bool b && b;
            scope.Publish(Key, !odd);
            return odd ? "odd" : "even";
        }
    }
}
