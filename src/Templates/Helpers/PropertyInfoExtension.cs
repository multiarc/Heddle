using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Templates.Helpers {
    internal static class PropertyInfoExtension {
        public static bool IsHaveAttribute (this PropertyInfo property, Type attributeType)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            return property.GetCustomAttributes(false).Any(a => a.GetType() == attributeType);
        }

        public static bool IsHaveAttribute<T> (this PropertyInfo property)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            return property.GetCustomAttributes(false).Any(a => a is T);
        }

        public static IEnumerable<T> GetAttributes<T> (this PropertyInfo type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return type.GetCustomAttributes(false).Where(a => a is T).Cast<T>();
        }

        public static IEnumerable GetAttributes (this PropertyInfo type, Type attributeType)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return type.GetCustomAttributes(false).Where(a => a.GetType() == attributeType);
        }
    }
}