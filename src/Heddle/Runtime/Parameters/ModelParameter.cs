using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Heddle.Data;
using Heddle.Language.Members;

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
            var body = BuildNullSafePropertyChain(inputParameter, getModelParameter);
            return Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), inputParameter);
        }

        /// <summary>
        /// Builds the null-safe property-hop chain over <paramref name="objectInput"/> (an object-typed
        /// expression). A hop off a null reference yields <c>default(T)</c> of the hop's property type.
        /// Shared by the member tier and the native-expression tier so both hop identically.
        /// <para>Phase 4 D8: the branch decision itself is <see cref="MemberHopRule.Form"/>, the same function the
        /// generator's <c>MemberPathWriter</c> maps to text. The two null-guarded forms collapse to one expression
        /// shape here — the split only matters where C#'s <c>?.</c> would widen a non-nullable value property to
        /// <c>Nullable&lt;T&gt;</c>, which expression trees never do.</para>
        /// </summary>
        internal static Expression BuildNullSafePropertyChain(Expression objectInput,
            IEnumerable<(Type type, PropertyInfo property)> getModelParameter)
        {
            Expression result = null;
            foreach (var parameter in getModelParameter)
            {
                var input = result ?? Expression.Convert(objectInput, parameter.type);
                var propertyType = parameter.property.PropertyType;
                var form = MemberHopRule.Form(parameter.type.IsValueType,
                    propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null);
                if (form == HopForm.Direct)
                {
                    result = Expression.MakeMemberAccess(input, parameter.property);
                }
                else
                {
                    result = Expression.Condition(
                        Expression.Equal(input,
                            Expression.Constant(null, parameter.type)
                        ), Expression.Default(propertyType),
                        Expression.MakeMemberAccess(input, parameter.property));
                }
            }

            if (result == null)
                throw new ArgumentException();

            return result;
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