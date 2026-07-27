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
        /// Builds the null-safe property-hop chain over <paramref name="objectInput"/>.
        /// Shared by the member tier and the native-expression tier so both hop identically using <see cref="MemberHopRule.Form"/>.
        /// <para>A null-safe hop names its receiver twice — once to test it, once to read through it — so each
        /// receiver is bound to a local first. Inlining it instead would place a second copy of everything below the
        /// hop into the tree, doubling both its size and the number of getter calls at every segment.</para>
        /// </summary>
        internal static Expression BuildNullSafePropertyChain(Expression objectInput,
            IEnumerable<(Type type, PropertyInfo property)> getModelParameter)
        {
            var receivers = new List<ParameterExpression>();
            var bindings = new List<Expression>();
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
                    var receiver = Expression.Variable(input.Type);
                    receivers.Add(receiver);
                    bindings.Add(Expression.Assign(receiver, input));
                    result = Expression.Condition(
                        Expression.Equal(receiver,
                            Expression.Constant(null, parameter.type)
                        ), Expression.Default(propertyType),
                        Expression.MakeMemberAccess(receiver, parameter.property));
                }
            }

            if (result == null)
                throw new ArgumentException();

            if (receivers.Count == 0)
                return result;

            bindings.Add(result);
            return Expression.Block(receivers, bindings);
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