using System;
using Heddle.Data;
using Heddle.Runtime.Parameters;

namespace Heddle.Runtime
{
    /// <summary>
    /// The per-call-site props binder (D7). Holds a frozen prototype array (defaults + constant arguments,
    /// converted and boxed once at compile) and a dynamic slot plan. When the plan is empty the frozen array
    /// itself is returned — shared across invocations, renders, and threads, never written after construction.
    /// Otherwise a per-invocation clone plus one store per dynamic slot, each argument evaluated against the
    /// caller view (<c>scope.Parent()</c>, D8).
    /// </summary>
    internal sealed class PropsBinder
    {
        private readonly object[] _frozen;
        private readonly DynamicSlot[] _plan;

        internal PropsBinder(object[] frozenPrototype, DynamicSlot[] dynamicPlan)
        {
            _frozen = frozenPrototype;
            _plan = dynamicPlan;
        }

        internal bool IsAllConstant => _plan.Length == 0;

        internal object[] Bind(in Scope scope)
        {
            if (_plan.Length == 0)
                return _frozen;

            var props = (object[]) _frozen.Clone();
            var callerView = scope.Parent();
            foreach (var entry in _plan)
            {
                var raw = entry.Parameter.GetParameter(callerView);
                props[entry.Index] = entry.Convert == null ? raw : entry.Convert(raw);
            }

            return props;
        }

        internal readonly struct DynamicSlot
        {
            public DynamicSlot(int index, IRuntimeParameter parameter, Func<object, object> convert)
            {
                Index = index;
                Parameter = parameter;
                Convert = convert;
            }

            public int Index { get; }
            public IRuntimeParameter Parameter { get; }
            public Func<object, object> Convert { get; }
        }
    }
}
