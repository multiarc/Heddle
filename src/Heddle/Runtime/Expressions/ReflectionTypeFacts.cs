using System;
using Heddle.Helpers;
using Heddle.Language.Binding;
using Heddle.Language.Expressions;
using Heddle.Precompiled;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// Phase 3 (F6): the <b>reflection</b> adapter of <see cref="ITypeFacts{TType}"/> — a thin veneer over
    /// <see cref="Type"/>. It carries no corrections: reflection <em>is</em> the authority the Roslyn adapter is
    /// corrected toward.
    /// </summary>
    internal sealed class ReflectionTypeFacts : ITypeFacts<Type>
    {
        internal static readonly ReflectionTypeFacts Instance = new ReflectionTypeFacts();

        private ReflectionTypeFacts() { }

        public bool IsAssignableFrom(Type target, Type source) =>
            target != null && source != null && target.IsAssignableFrom(source);

        public bool TryGetNullableUnderlying(Type type, out Type underlying)
        {
            underlying = type == null ? null : Nullable.GetUnderlyingType(type);
            return underlying != null;
        }

        public bool IsInterface(Type type) => type != null && type.IsInterface;

        public bool IsValueType(Type type) => type != null && type.IsValueType;

        public bool IsUsableAsPropType(Type type) =>
            type != null && !type.ContainsGenericParameters && !type.IsPointer && !type.IsByRef;

        public NumericKind GetNumericKind(Type type) => NumericTable.FromClrType(type);

        public bool IsObject(Type type) => type == typeof(object);

        public string FormatAqn(Type type) => ReflectionTypeIdentity.AqnSansVersion(type);

        public string Display(Type type) => type == null ? "<unknown>" : type.ToString();
    }
}
