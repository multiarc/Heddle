using System;

namespace Templates.Helpers {
    /// <summary>
    /// Smart Type defenition Reference Resolvable at any time.
    /// </summary>
    public class TypeReference
    {
        public TypeReference()
        {
            _default = typeof (object);
        }

        public TypeReference(Type nonResolvedType)
        {
            _default = nonResolvedType;
        }

        private volatile bool _resolved;
        private Type _typeValue;
        private readonly Type _default;

        public Type TypeValue
        {
            get
            {
                if (_resolved)
                    return _typeValue;
                return _default;
            }
        }

        public void ResolveTypeReference(Type typeValue)
        {
            if (!_resolved)
            {
                _typeValue = typeValue;
                _resolved = true;
            }
            else
            {
                //throw new TemplateInitException(string.Format("The type [{0}] was already resolved. Avoid rewriting type resolver.", _typeValue));
            }
        }

        public static implicit operator TypeReference(Type type)
        {
            if (type == null)
                return null;
            return new TypeReference(type);
        }

        public static implicit operator Type(TypeReference type)
        {
            if (type == null)
                return null;
            return type.TypeValue;
        }
    }
}
