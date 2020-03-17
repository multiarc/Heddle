using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class ModelParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public ModelParameter(KeyValuePair<Type, PropertyInfo>[] getModelParameter)
        {
            _compiledAccessor = GetPropertyChainAccessor(getModelParameter).Compile();
        }

        internal static Expression<Func<object, object>> GetPropertyChainAccessor(KeyValuePair<Type, PropertyInfo>[] getModelParameter)
        {
            var inputParameter = Expression.Parameter(typeof(object));

            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            Expression result = null;
            foreach (var parameter in getModelParameter)
            {
                var input = result ?? Expression.Convert(inputParameter, parameter.Key);
                result = Expression.Condition(
                    Expression.Equal(input,
                        Expression.Constant(null, parameter.Key)
                    ), Expression.Default(parameter.Value.PropertyType),
                    Expression.MakeMemberAccess(input, parameter.Value));
            }

            if (result == null)
                throw new ArgumentException();

            return Expression.Lambda<Func<object, object>>(Expression.Convert(result, typeof(object)), inputParameter);
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            return _compiledAccessor(scope.ModelData);
        }
    }
}