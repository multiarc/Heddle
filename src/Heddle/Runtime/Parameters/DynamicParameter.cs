using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Heddle.Data;

namespace Heddle.Runtime.Parameters
{
    internal class DynamicParameter : IRuntimeParameter
    {
        private readonly IRuntimeParameter _source;
        private readonly Func<object, object> _compiledAccessor;

        public DynamicParameter(IEnumerable<string> names, IRuntimeParameter source = null)
        {
            _source = source;
            _compiledAccessor = GetDynamicPropertyChainAccessor(names).Compile();
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The dynamic tier is outside the AOT claim; reached only for dynamic scopes, which the printer declines and strict load refuses.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The dynamic tier is outside the AOT claim; reached only for dynamic scopes, which the printer declines and strict load refuses.")]
        internal static Expression<Func<object, object>> GetDynamicPropertyChainAccessor(IEnumerable<string> names)
        {
            var inputParameter = Expression.Parameter(typeof(object));

            // Each hop tests its receiver and then binds through it, so the receiver is held in a local: naming it
            // twice in the tree would duplicate every hop below it, and with it the call site and the member read.
            var receivers = new List<ParameterExpression>();
            var bindings = new List<Expression>();
            Expression result = null;
            foreach (var name in names)
            {
                var binder = Binder.GetMember(CSharpBinderFlags.None, name, typeof(DynamicParameter), new[]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                });
                var receiver = Expression.Variable(typeof(object));
                receivers.Add(receiver);
                bindings.Add(Expression.Assign(receiver, result ?? inputParameter));
                result = Expression.Condition(
                    Expression.Equal(receiver,
                        Expression.Constant(null, typeof(object))
                    ), Expression.Constant(null, typeof(object)),
                    DynamicExpression.Dynamic(binder, typeof(object), receiver));
            }

            if (result == null)
                throw new ArgumentException();

            bindings.Add(result);
            return Expression.Lambda<Func<object, object>>(Expression.Block(receivers, bindings), inputParameter);
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            return _compiledAccessor(_source != null ? _source.GetParameter(scope) : scope.ModelData);
        }
    }
}