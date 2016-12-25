using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class DynamicParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public DynamicParameter(string[] names)
        {
            _compiledAccessor = GetDynamicPropertyChainAccessor(names).Compile();
        }

        internal static Expression<Func<object, object>> GetDynamicPropertyChainAccessor(string[] names)
        {
            var inputParameter = Expression.Parameter(typeof(object));

            Expression result = null;
            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
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

            if (result == null)
                throw new ArgumentException();

            return Expression.Lambda<Func<object, object>>(result, inputParameter);
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(ref Scope scope)
        {
            return _compiledAccessor(scope.ModelData);
        }
    }
}