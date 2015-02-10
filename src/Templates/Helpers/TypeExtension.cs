using System;
using System.Linq;

namespace Templates.Helpers {
    public static class TypeExtension {
        public static bool IsType(this Type typeToCheck, Type type)
        {
            if (typeToCheck == null)
                throw new ArgumentNullException("typeToCheck");

            if (type == null)
                throw new ArgumentNullException("type");

            return typeToCheck.IsAssignableFrom(type);
        }

        public static bool IsType(this Type typeToCheck, object data)
        {
            if (typeToCheck == null)
                throw new ArgumentNullException("typeToCheck");

            if (data == null)
                throw new ArgumentNullException("data");

            return typeToCheck.IsInstanceOfType(data);
        }


        public static bool IsType<T>(this Type typeToCheck)
        {
            if (typeToCheck == null)
                throw new ArgumentNullException("typeToCheck");

            return typeToCheck.IsAssignableFrom(typeof (T));
        }

        public static bool IsImplement(this Type type, Type interfaceType)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (interfaceType == null)
                throw new ArgumentNullException("interfaceType");

            return type.GetInterfaces().Any(i => i == interfaceType);
        }

        public static bool IsImplement<T>(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return type.GetInterfaces().Any(i => i == typeof (T));
        }

        public static bool IsHaveAttribute(this Type type, Type attributeType)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (attributeType == null)
                throw new ArgumentNullException("attributeType");

            return type.GetCustomAttributes(false).Any(a => a.GetType() == attributeType);
        }

        public static bool IsHaveAttribute<T>(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return type.GetCustomAttributes(false).Any(a => a is T);
        }

        public static T[] GetAttributes<T>(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return type.GetCustomAttributes(false).Where(a => a is T).Cast<T>().ToArray();
        }
    }
}