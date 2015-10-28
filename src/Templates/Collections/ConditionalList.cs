using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Templates.Collections
{
    public class ConditionalList<T> : SmartList<T>
    {
        private readonly Func<T, bool> _addCondition;

        public ConditionalList(Func<T, bool> addCondition)
        {
            if (addCondition == null)
                throw new ArgumentNullException(nameof(addCondition));
            _addCondition = addCondition;
        }

        public ConditionalList(Func<T, bool> addCondition, T[] value):base(value)
        {
            if (addCondition == null)
                throw new ArgumentNullException(nameof(addCondition));
            _addCondition = addCondition;
        }

        public ConditionalList(Func<T, bool> addCondition, int capacity) : base(capacity)
        {
            if (addCondition == null)
                throw new ArgumentNullException(nameof(addCondition));
            _addCondition = addCondition;
        }

        public override void Add(T value)
        {
            if (_addCondition(value))
                base.Add(value);
        }

        public override int Add(object value)
        {
            if (value is T)
            {
                if (_addCondition((T) value))
                    return base.Add(value);
            }
            if (ReferenceEquals(null, value))
            {
                if (_addCondition(default(T)))
                    return base.Add(null);
            }
            return -1;
        }
    }
}
