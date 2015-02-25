using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Extensions;
using Templates.Helpers;
using Templates.Runtime;
using Templates.Strings;

namespace Templater.Tests {
    [TestClass]
    public class ReflectionHelperTest {
        public TestContext TestContext
        {
            get;
            set;
        }

        [TestMethod]
        public void ReflectionHelperConstructorTest ()
        {
            Type innerType = typeof (ExString);
            var target = new ReflectionHelper(innerType);
            Assert.AreEqual(target.InnerType, innerType);
        }

        [TestMethod]
        public void ReflectionHelperConstructorTest1 ()
        {
            object value = new ExString();
            var target = new ReflectionHelper(value);
            Assert.AreEqual(target.InnerType, value.GetType());
        }

        [TestMethod]
        public void GetIsImplementTest ()
        {
            Type innerType = typeof (ListExtension);
            var target = new ReflectionHelper(innerType);
            Type type = typeof (IExtension);
            Assert.AreEqual(true, target.IsImplement(type));
            type = typeof (IDisposable);
            Assert.AreEqual(true, target.IsImplement(type));
        }

        [TestMethod]
        public void IsClassTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            bool actual = target.IsClass;
            Assert.AreEqual(true, actual);
            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            actual = target.IsClass;
            Assert.AreEqual(false, actual);
            innerType = typeof (IDisposable);
            target = new ReflectionHelper(innerType);
            actual = target.IsClass;
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void IsInterfaceTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            bool actual = target.IsInterface;
            Assert.AreEqual(false, actual);
            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            actual = target.IsInterface;
            Assert.AreEqual(false, actual);
            innerType = typeof (IDisposable);
            target = new ReflectionHelper(innerType);
            actual = target.IsInterface;
            Assert.AreEqual(true, actual);
        }

        [TestMethod]
        public void IsObjectTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            bool actual = target.IsObject;
            Assert.AreEqual(true, actual);
            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            actual = target.IsObject;
            Assert.AreEqual(false, actual);
            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            actual = target.IsObject;
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void IsTypeTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            var value = new object();
            bool actual = target.IsType(value);
            Assert.AreEqual(true, actual);
            value = new ExString();
            actual = target.IsType(value);
            Assert.AreEqual(true, actual);
            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = new ListExtension();
            actual = target.IsType(value);
            Assert.AreEqual(true, actual);
            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = new object();
            actual = target.IsType(value);
            Assert.AreEqual(false, actual);
            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = new object();
            actual = target.IsType(value);
            Assert.AreEqual(false, actual);
            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = new int();
            actual = target.IsType(value);
            Assert.AreEqual(true, actual);
        }

        [TestMethod]
        public void IsTypeTest1 ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            Type value = typeof (object);
            bool actual = target.IsType(value);
            Assert.AreEqual(true, actual);
            value = typeof (ExString);
            actual = target.IsType(value);
            Assert.AreEqual(true, actual);
            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = typeof (ListExtension);
            actual = target.IsType(value);
            Assert.AreEqual(true, actual);
            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = typeof (object);
            actual = target.IsType(value);
            Assert.AreEqual(false, actual);
            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = typeof (object);
            actual = target.IsType(value);
            Assert.AreEqual(false, actual);
            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = typeof (int);
            actual = target.IsType(value);
            Assert.AreEqual(true, actual);
            innerType = typeof (object);
            target = new ReflectionHelper(innerType);
            value = typeof (Type);
            actual = target.IsType(value);
            Assert.AreEqual(true, actual);
        }
    }
}