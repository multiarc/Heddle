using System;
using System.Globalization;
using Heddle.Data;

namespace Heddle.Runtime.Expressions
{
    /// <summary>Converts props via identity, numeric widening, nullable lifting, reference assignability, boxing, and null-literal rules.
    /// Used for defaults (HED5009), named-argument checks (HED5003), and slot-value checks with <paramref name="allowBoxToObject"/> = false.</summary>
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
                return true;

            if (allowBoxToObject && t == typeof(object) && s.IsValueType)
                return true;

            if (NumericPromotion.IsImplicitNumeric(s, t))
                return true;

            var tUnder = Nullable.GetUnderlyingType(t);
            if (tUnder != null)
            {
                if (s == tUnder)
                    return true;
                if (s.IsValueType && NumericPromotion.IsImplicitNumeric(s, tUnder))
                    return true;
                var sUnder = Nullable.GetUnderlyingType(s);
                if (sUnder != null && (sUnder == tUnder || NumericPromotion.IsImplicitNumeric(sUnder, tUnder)))
                    return true;
            }

            if (!s.IsValueType && t.IsAssignableFrom(s))
                return true;                                   // Reference types only (value types handled above).

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
