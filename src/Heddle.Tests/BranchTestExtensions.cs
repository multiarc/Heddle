using System;
using System.Collections.Generic;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Tests
{
    /// <summary>
    /// Test extensions that observe the local-context frame and <see cref="Scope.Publish"/>/<see cref="Scope.TryRead"/> channel.
    /// Registered via <see cref="TemplateFactory.AddExtensions"/> to avoid assembly-scan timing.
    /// </summary>
    internal static class BranchTestExtensions
    {
        private static readonly object Gate = new object();
        private static bool _registered;

        public static void Register()
        {
            lock (Gate)
            {
                if (_registered)
                    return;
                Add("probe", typeof(ScopeProbeExtension));
                Add("plainprobe", typeof(PlainProbeExtension));
                Add("branchreader", typeof(BranchReaderExtension));
                Add("branchdriver", typeof(BranchDriverExtension));
                _registered = true;
            }
        }

        private static void Add(string name, Type type)
        {
            if (!TemplateFactory.Exists(name))
                TemplateFactory.AddExtensions(new[] { new ExtensionType(name, type, false) });
        }
    }

    /// <summary>A <c>[ScopeChannel]</c> participant that records the frame it executes under (per body).</summary>
    [ExtensionName("probe")]
    [ScopeChannel]
    public class ScopeProbeExtension : AbstractExtension
    {
        [ThreadStatic] private static List<object> _frames;

        public static IReadOnlyList<object> Frames => _frames ?? new List<object>();

        public static void Reset() => _frames = new List<object>();

        public override object ProcessData(in Scope scope)
        {
            Record(scope);
            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            Record(scope);
        }

        private static void Record(in Scope scope)
        {
            (_frames ??= new List<object>()).Add(scope.Locals);
        }
    }

    /// <summary>A non-participant probe (no <c>[ScopeChannel]</c>) that records the frame it executes under.</summary>
    [ExtensionName("plainprobe")]
    public class PlainProbeExtension : AbstractExtension
    {
        [ThreadStatic] private static List<object> _frames;

        public static IReadOnlyList<object> Frames => _frames ?? new List<object>();

        public static void Reset() => _frames = new List<object>();

        public override object ProcessData(in Scope scope)
        {
            Record(scope);
            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            Record(scope);
        }

        private static void Record(in Scope scope)
        {
            (_frames ??= new List<object>()).Add(scope.Locals);
        }
    }

    /// <summary>
    /// A <c>[ScopeChannel]</c> reader that renders the current branch-set state through the public channel:
    /// <c>SAT</c>/<c>UNSAT</c> when a <see cref="BranchState"/> is published, <c>NONE</c> otherwise.
    /// </summary>
    [ExtensionName("branchreader")]
    [ScopeChannel]
    public class BranchReaderExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => Read(scope);

        public override void RenderData(in Scope scope) => scope.Renderer.Render(Read(scope));

        private static string Read(in Scope scope)
        {
            if (scope.TryRead(BranchState.ReservedKey, out var value) && value is BranchState state)
                return state.Satisfied ? "SAT" : "UNSAT";
            return "NONE";
        }
    }

    /// <summary>
    /// A <c>[ScopeChannel]</c> publisher that drives a branch set by publishing <c>new BranchState(true)</c>;
    /// silences a following <c>@else</c> branch.
    /// </summary>
    [ExtensionName("branchdriver")]
    [ScopeChannel]
    public class BranchDriverExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            scope.Publish(BranchState.ReservedKey, new BranchState(true));
            return string.Empty;
        }

        public override void RenderData(in Scope scope)
        {
            scope.Publish(BranchState.ReservedKey, new BranchState(true));
        }
    }
}
