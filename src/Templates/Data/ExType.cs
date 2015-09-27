using System;
using System.CodeDom;
using Microsoft.CSharp;
using Templates.Helpers;

namespace Templates.Data {
    public class ExType: IEquatable<ExType>
    {
        private const string DynamicConst = "dynamic";

        public static ExType Dynamic { get; }
            = new ExType(typeof (object), DynamicConst);

        private readonly bool _isDynamic;

        public bool IsDynamic => _isDynamic;

        public Type Type { get; }

        public ExType(Type type, string name) {
            Type = type;
            _isDynamic = name == DynamicConst;
        }

        public ExType(Type type) {
            if (type == null) throw new ArgumentNullException(nameof(type));
            Type = type;
            _isDynamic = false;
        }

        public ExType(string name, params string[] imports) {
            _isDynamic = name == DynamicConst;
            Type = _isDynamic ? Dynamic.Type : ReflectionHelper.ResolveType(name, imports ?? new string[0]);
        }

        public static implicit operator ExType(Type type) {
            if ((object)type == null)
                return null;
            return new ExType(type);
        }

        public static explicit operator Type(ExType type) {
            if ((object)type == null)
                return null;
            return type.Type;
        }

        public override int GetHashCode() {
            return _isDynamic ? DynamicConst.GetHashCode() : Type?.GetHashCode() ?? string.Empty.GetHashCode();
        }

        public override string ToString()
        {
            if (_isDynamic)
                return DynamicConst;

            return Type.GetTypeOutput();
        }

        public override bool Equals(object obj) {
            if (!(obj is ExType))
                return false;
            return Equals(this, (ExType)obj);
        }

        public static bool Equals(ExType one, ExType another) {
            if (ReferenceEquals(one, another))
                return true;
            if ((object)one == null || (object)another == null) {
                return false;
            }
            return one.Type == another.Type && one._isDynamic == another._isDynamic;
        }

        public bool Equals(ExType other) {
            if ((object)other == null)
                return false;
            return Equals(this, other);
        }

        public static bool operator ==(ExType one, ExType another) {
            return Equals(one, another);
        }

        public static bool operator !=(ExType one, ExType another) {
            return !Equals(one, another);
        }
    }
}
