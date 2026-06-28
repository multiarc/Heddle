using System;
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

        internal static Expression<Func<object, object>> GetDynamicPropertyChainAccessor(IEnumerable<string> names)
        {
            var inputParameter = Expression.Parameter(typeof(object));

            Expression result = null;
            foreach (var name in names)
            {
                var binder = Binder.GetMember(CSharpBinderFlags.None, name, typeof(DynamicParameter), new[]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                });
                var input = result ?? inputParameter;
                result = Expression.Condition(
                    Expression.Equal(input,
                        Expression.Constant(null, typeof(object))
                    ), Expression.Constant(null, typeof(object)),
                    DynamicExpression.Dynamic(binder, typeof(object), input));
            }

            if (result == null)
                throw new ArgumentException();

            return Expression.Lambda<Func<object, object>>(result, inputParameter);
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