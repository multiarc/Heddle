using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions(
    typeof(Heddle.Samples.CustomExtensions.StashExtension),
    typeof(Heddle.Samples.CustomExtensions.RecallExtension),
    typeof(Heddle.Samples.CustomExtensions.IfMissExtension))]

namespace Heddle.Samples.CustomExtensions
{
    // A third-party-style pair coordinating through the public Scope.Publish/TryRead channel (phase 3 D6): the
    // publisher writes under its own key (respecting the reserved "heddle." prefix rule) and the reader retrieves it.
    // [ScopeChannel] tells the engine a body containing this extension needs a local frame.

    [ExtensionName("stash")]
    [ScopeChannel]
    public sealed class StashExtension : AbstractExtension
    {
        public const string Key = "sample.value";   // an ordinary (non-reserved) channel key

        public override object ProcessData(in Scope scope)
        {
            scope.Publish(Key, scope.ModelData);
            return string.Empty;
        }

        public override void RenderData(in Scope scope) => scope.Publish(Key, scope.ModelData);
    }

    [ExtensionName("recall")]
    [ScopeChannel]
    public sealed class RecallExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) =>
            scope.TryRead(StashExtension.Key, out var value) ? value ?? string.Empty : "(nothing published)";

        public override void RenderData(in Scope scope) =>
            scope.Renderer.Render(ProcessData(scope)?.ToString() ?? string.Empty);
    }

    // A branch-protocol participant (phase 3): reads the BranchState published by a preceding @if/@elif and renders
    // its body only on the not-taken path — a third-party @else-like extension built entirely on the public channel.
    [ExtensionName("ifmiss")]
    [ScopeChannel]
    public sealed class IfMissExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            if (NotTaken(scope))
                return GetInnerResult(scope.Parent());
            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            if (NotTaken(scope))
                RenderInnerResult(scope.Parent());
        }

        // Reads the branch protocol through the PUBLIC channel: the reserved key carries a BranchState.
        private static bool NotTaken(in Scope scope) =>
            scope.TryRead(BranchState.ReservedKey, out var value) && value is BranchState state && !state.Satisfied;
    }
}
