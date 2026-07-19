using System;
using System.Globalization;
using Heddle.Data;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// The single implementation of the D10 rule-4 conversion set (identity; implicit numeric widening;
    /// <c>T</c>→<c>T?</c> lifting; reference assignability; boxing to <see cref="object"/>; the <c>null</c>
    /// literal). Used verbatim for prop defaults (HED5009), named-argument type checks (HED5003), and — with
    /// <paramref name="allowBoxToObject"/> = false — slot-value checks (HED5014). Keeps prop-narrowing and
    /// model-narrowing from drifting by reusing <see cref="NumericPromotion"/> and reflection assignability.
    /// </summary>
    internal static class PropConversion
    {
        /// <summary>Static-type check: can <paramref name="source"/> convert to <paramref name="target"/>?</summary>
        internal static bool CanConvert(ExType source, ExType target, bool allowBoxToObject)
        {
            if (source == null || target == null || source.IsDynamic || target.IsDynamic)
                return false;
            return CanConvertTypes(source.Type, target.Type, allowBoxToObject);
        }

        internal static bool CanConvertTypes(Type s, Type t, bool allowBoxToObject)
        {
            if (s == null || t == null)
                return false;
            if (s == t)
                return true;                                   // identity

            if (allowBoxToObject && t == typeof(object) && s.IsValueType)
                return true;                                   // boxing to object

            if (NumericPromotion.IsImplicitNumeric(s, t))
                return true;                                   // implicit numeric widening

            var tUnder = Nullable.GetUnderlyingType(t);
            if (tUnder != null)                                // target is Nullable<W>
            {
                if (s == tUnder)
                    return true;                               // identity-lift
                if (s.IsValueType && NumericPromotion.IsImplicitNumeric(s, tUnder))
                    return true;                               // widen-then-lift
                var sUnder = Nullable.GetUnderlyingType(s);
                if (sUnder != null && (sUnder == tUnder || NumericPromotion.IsImplicitNumeric(sUnder, tUnder)))
                    return true;                               // Nullable<S> -> Nullable<W>
            }

            if (!s.IsValueType && t.IsAssignableFrom(s))
                return true;                                   // reference assignability (never value types)

            return false;
        }

        /// <summary>
        /// Converts a literal default value (or the <c>null</c> literal when <paramref name="isNull"/>) to the
        /// declared <paramref name="target"/> type, producing the boxed value stored in the frozen prototype.
        /// Returns false when the default is not convertible (HED5009).
        /// </summary>
        internal static bool TryConvertLiteral(object value, bool isNull, Type target, out object converted)
        {
            converted = null;
            if (isNull || value == null)
            {
                if (!target.IsValueType || Nullable.GetUnderlyingType(target) != null)
                {
                    converted = null;
                    return true;
                }

                return false;
            }

            var source = value.GetType();
            if (!CanConvertTypes(source, target, allowBoxToObject: true))
                return false;

            converted = ConvertValue(value, source, target);
            return true;
        }

        /// <summary>
        /// Performs the runtime value conversion for an already-validated conversion: numeric widenings change
        /// the boxed value's type (to the target's underlying), everything else passes through unchanged (a
        /// boxed <c>T</c> already serves as <c>T?</c>, <c>object</c>, or a base reference type).
        /// </summary>
        internal static object ConvertValue(object value, Type source, Type target)
        {
            if (value == null)
                return null;
            var targetUnderlying = Nullable.GetUnderlyingType(target) ?? target;
            var sourceUnderlying = Nullable.GetUnderlyingType(source) ?? source;
            if (sourceUnderlying != targetUnderlying &&
                NumericPromotion.IsImplicitNumeric(sourceUnderlying, targetUnderlying))
            {
                return Convert.ChangeType(value, targetUnderlying, CultureInfo.InvariantCulture);
            }

            return value;
        }
    }
}
