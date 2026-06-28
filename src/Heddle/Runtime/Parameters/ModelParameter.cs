using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Heddle.Data;

namespace Heddle.Runtime.Parameters
{
    internal class ModelParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public ModelParameter(IEnumerable<(Type, PropertyInfo)> getModelParameter)
        {
            _compiledAccessor = GetPropertyChainAccessor(getModelParameter).Compile();
        }

        internal static Expression<Func<object, object>> GetPropertyChainAccessor(
            IEnumerable<(Type type, PropertyInfo property)> getModelParameter)
        {
            var inputParameter = Expression.Parameter(typeof(object));

            Expression result = null;
            foreach (var parameter in getModelParameter)
            {
                var input = result ?? Expression.Convert(inputParameter, parameter.type);
                if (parameter.type.IsValueType)
                {
                    result = Expression.MakeMemberAccess(input, parameter.property);
                }
                else
                {
                    result = Expression.Condition(
                        Expression.Equal(input,
                            Expression.Constant(null, parameter.type)
                        ), Expression.Default(parameter.property.PropertyType),
                        Expression.MakeMemberAccess(input, parameter.property));
                }
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