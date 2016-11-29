using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class ModelParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public ModelParameter(PropertyInfo[] getModelParameter)
        {
            var inputParameter = Expression.Parameter(typeof(object));

            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            Expression result = null;
            for (var i = 0; i < getModelParameter.Length; i++)
            {
                var input = result ?? Expression.Convert(inputParameter, getModelParameter[i].DeclaringType);
                result = Expression.Condition(
                    Expression.Equal(input,
                        Expression.Constant(null, getModelParameter[i].DeclaringType)
                    ), Expression.Default(getModelParameter[i].PropertyType), Expression.MakeMemberAccess(input, getModelParameter[i]));
            }
            _compiledAccessor =
                Expression.Lambda<Func<object, object>>(Expression.Convert(result, typeof(object)), inputParameter).Compile();
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