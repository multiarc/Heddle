using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Helpers;
using Type = System.Type;

namespace Templater.Tests {
    [TestClass]
    public class AttributeSetTest {
        public TestContext TestContext
        {
            get;
            set;
        }

        public void GetAttributeTestHelper<TAttribute, TType, TNotExists> ()
        {
            Type type = typeof (TType);
            var target = new AttributeSet(type);
            var actual = target.GetAttribute<TAttribute>();
            var another = target.GetAttribute<TNotExists>();
            Assert.IsNotNull(actual);
            Assert.IsNull(another);
        }

        [TestMethod]
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
            Assert.IsNotNull(actual);
            Assert.IsTrue(actual.Count != 0);
            List<TNotExists> another = target.GetAttributes<TNotExists>().ToList();
            Assert.IsNotNull(another);
            Assert.IsTrue(another.Count == 0);
        }

        [TestMethod]
        public void GetAttributesTest ()
        {
            GetAttributesTestHelper<SerializableAttribute, object, SecuritySafeCriticalAttribute>();
            GetAttributesTestHelper<ComVisibleAttribute, string, SecuritySafeCriticalAttribute>();
        }

        public void GetIsPresentAttributeHelper<TType> (Type attribute, Type notExisingAttribute)
        {
            Type type = typeof (TType);
            var target = new AttributeSet(type);
            bool actual = target.GetIsPresentAttribute(attribute);
            Assert.IsTrue(actual);
            actual = target.GetIsPresentAttribute(notExisingAttribute);
            Assert.IsFalse(actual);
        }

        [TestMethod]
        public void GetIsPresentAttributeTest ()
        {
            GetIsPresentAttributeHelper<object>(typeof (SerializableAttribute), typeof (SecuritySafeCriticalAttribute));
            GetIsPresentAttributeHelper<string>(typeof (ComVisibleAttribute), typeof (SecuritySafeCriticalAttribute));
        }

        [TestMethod]
        public void AllAttributesTest ()
        {
            Type type = typeof (string);
            var target = new AttributeSet(type);
            ReadOnlyCollection<Attribute> actual = target.AllAttributes;
            Assert.IsTrue(actual.Count == 4);
        }

        [TestMethod]
        public void ConstructDifferenceTest ()
        {
            Type type = typeof (string);
            var target = new AttributeSet(type);
            ReadOnlyCollection<Attribute> actual = target.AllAttributes;
            Assert.IsTrue(actual.Count == 4);
            target = new AttributeSet(type.GetCustomAttributes(false));
            ReadOnlyCollection<Attribute> expected = target.AllAttributes;
            for (int i = 0; i < 0; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }
    }
}