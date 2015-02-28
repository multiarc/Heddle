using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using Templates.Helpers;
using Xunit;

namespace TemplatesXTests {
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

        [Fact]
        public void GetAttributeTest ()
        {
            GetAttributeTestHelper<SerializableAttribute, object, SecuritySafeCriticalAttribute>();
            GetAttributeTestHelper<ComVisibleAttribute, string, SecuritySafeCriticalAttribute>();
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

        [Fact]
        public void GetAttributesTest ()
        {
            GetAttributesTestHelper<SerializableAttribute, object, SecuritySafeCriticalAttribute>();
            GetAttributesTestHelper<ComVisibleAttribute, string, SecuritySafeCriticalAttribute>();
        }

        public void GetIsPresentAttributeHelper<TType> (Type attribute, Type notExisingAttribute)
        {
            Type type = typeof (TType);
            var target = new AttributeSet(type);
            Assert.True(target.GetIsPresentAttribute(attribute));

            Assert.False(target.GetIsPresentAttribute(notExisingAttribute));
        }

        [Fact]
        public void GetIsPresentAttributeTest ()
        {
            GetIsPresentAttributeHelper<object>(typeof (SerializableAttribute), typeof (SecuritySafeCriticalAttribute));
            GetIsPresentAttributeHelper<string>(typeof (ComVisibleAttribute), typeof (SecuritySafeCriticalAttribute));
        }

        [Fact]
        public void AllAttributesTest ()
        {
            Type type = typeof (string);
            var target = new AttributeSet(type);
            ReadOnlyCollection<Attribute> actual = target.AllAttributes;
            Assert.True(actual.Count == 4);
        }

        [Fact]
        public void ConstructDifferenceTest ()
        {
            Type type = typeof (string);
            var target = new AttributeSet(type);
            ReadOnlyCollection<Attribute> actual = target.AllAttributes;
            Assert.True(actual.Count == 4);

            target = new AttributeSet(type.GetCustomAttributes(false));
            ReadOnlyCollection<Attribute> expected = target.AllAttributes;
            for (int i = 0; i < 0; i++)
                Assert.Equal(expected[i], actual[i]);
        }
    }
}