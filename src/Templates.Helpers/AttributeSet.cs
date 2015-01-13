using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Templates.Helpers {
    /// <summary>
    /// Attribute set helper to simple check existance/get attribute of any type/property/field etc.
    /// </summary>
    public class AttributeSet {
        private List<Attribute> _attributes;

        public AttributeSet (Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            ParseAttributes(type.GetCustomAttributes(false));
        }

        public AttributeSet (IEnumerable<object> attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException("attributes");

            ParseAttributes(attributes);
        }

        /// <summary>
        /// Gets list of custom attributes of type/property/field etc.
        /// </summary>
        public ReadOnlyCollection<Attribute> AllAttributes
        {
            get { return _attributes.AsReadOnly(); }
        }

        private void ParseAttributes (IEnumerable<object> attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException("attributes");
            _attributes = new List<Attribute>();
            _attributes.AddRange(attributes.Cast<Attribute>());
        }

        /// <summary>
        /// Gets attribute existance in type
        /// </summary>
        /// <param name="attributeType">Attribute type to check</param>
        /// <returns>Returns true if attribute exists</returns>
        public bool GetIsPresentAttribute (Type attributeType)
        {
            if (attributeType == null)
                throw new ArgumentNullException("attributeType");

            return AllAttributes.Any(a => a.GetType() == attributeType);
        }

        /// <summary>
        /// Get attribute by type
        /// </summary>
        /// <typeparam name="T">Type indicating which attribute need to get</typeparam>
        /// <returns>First Attribute object found of type T</returns>
        public T GetAttribute<T> ()
        {
            return AllAttributes.Where(a => a is T).Cast<T>().FirstOrDefault();
        }

        /// <summary>
        /// Get typed attribute. Look into all custome attributes. Return collection of attributes found.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> GetAttributes<T> ()
        {
            return AllAttributes.Where(a => a is T).Cast<T>();
        }
    }
}