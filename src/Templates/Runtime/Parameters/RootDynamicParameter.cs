using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class RootDynamicParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public RootDynamicParameter(string[] names)
        {
            var inputParameter = Expression.Parameter(typeof(object));

            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            Expression result = null;

            for (var i = 0; i < names.Length; i++)
            {
                var binder = Binder.GetMember(CSharpBinderFlags.None, names[i], typeof(DynamicParameter), new[]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                });
                var input = result ?? inputParameter;
                result = Expression.Condition(
                    Expression.Equal(input,
                        Expression.Constant(null, typeof(object))
                    ), Expression.Constant(null, typeof(object)), DynamicExpression.Dynamic(binder, typeof(object), input));
            }
            _compiledAccessor =
                Expression.Lambda<Func<object, object>>(result, inputParameter).Compile();
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(ref Scope scope)
        {
            return _compiledAccessor(scope.RootData);
        }
    }
}