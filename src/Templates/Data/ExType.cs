using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Helpers;

namespace Templates.Data {
    public class ExType: IEquatable<ExType> {
        public static ExType Dynamic { get; }
        = new ExType("dynamic");

        private readonly string _name;

        private readonly bool _isDynamic;

        public bool IsDynamic {
            get {
                return _isDynamic;
            }
        }

        public Type Type { get; }

        public ExType(Type type) {
            Type = type;
            _name = type.ToString();
            _isDynamic = _name == "dynamic";
        }

        public ExType(string name, params string[] imports) {
            _name = name;
            _isDynamic = _name == "dynamic";
            Type = ReflectionHelper.ResolveType(name, imports);
        }

        public static implicit operator ExType(Type type) {
            if (type == null)
                return null;
            return new ExType(type);
        }

        public static explicit operator Type(ExType type) {
            if ((object)type == null)
                return null;
            return type.Type;
        }

        public override int GetHashCode() {
            return _name?.GetHashCode() ?? Type?.GetHashCode() ?? string.Empty.GetHashCode();
        }

        public override string ToString() {
            return _name ?? Type?.ToString() ?? string.Empty;
        }

        public override bool Equals(object obj) {
            if (obj == null || !(obj is ExType))
                return false;
            return Equals(this, (ExType)obj);
        }

        public static bool Equals(ExType one, ExType another) {
            if (one == (object)another)
                return true;
            if ((object)one == null || (object)another == null) {
                return false;
            }
            return one.Type == another.Type;
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
