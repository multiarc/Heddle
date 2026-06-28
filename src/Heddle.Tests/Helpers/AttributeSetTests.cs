using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Helpers;
using Xunit;

namespace Heddle.Tests {
    public class AttributeSetTest {

        public void GetAttributeTestHelper<TAttribute, TType, TNotExists> ()
        {
            Type type = typeof (TType);
            var target = new AttributeSet(type);
            var actual = target.GetAttribute<TAttribute>();
            var another = target.GetAttribute<TNotExists>();
            Assert.NotNull(actual);
            Assert.Null(another);
        }

        public void GetAttributesTestHelper<TAttribute, TType, TNotExists> ()
        {
            Type type = typeof (TType);
            var target = new AttributeSet(type);
            List<TAttribute> actual = target.GetAttributes<TAttribute>().ToList();
            Assert.NotNull(actual);
            Assert.True(actual.Count != 0);
            List<TNotExists> another = target.GetAttributes<TNotExists>().ToList();
            Assert.NotNull(another);
            Assert.True(another.Count == 0);
        }

        public void GetIsPresentAttributeHelper<TType> (Type attribute, Type notExisingAttribute)
        {
            Type type = typeof (TType);
            var target = new AttributeSet(type);
            Assert.True(target.GetIsPresentAttribute(attribute));

            Assert.False(target.GetIsPresentAttribute(notExisingAttribute));
        }
    }
}