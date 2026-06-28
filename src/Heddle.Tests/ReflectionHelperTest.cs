using System;
using Heddle.Extensions;
using Heddle.Helpers;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests {
    public class ReflectionHelperTest {
        [Fact]
        public void ReflectionHelperConstructorTest ()
        {
            Type innerType = typeof (string);
            var target = new ReflectionHelper(innerType);
            Assert.Equal(target.InnerType, innerType);
        }

        [Fact]
        public void ReflectionHelperConstructorTest1 ()
        {
            object value = new string('0', 1);
            var target = new ReflectionHelper(value);
            Assert.Equal(target.InnerType, value.GetType());
        }

        [Fact]
        public void GetIsImplementTest ()
        {
            Type innerType = typeof (ListExtension);
            var target = new ReflectionHelper(innerType);
            Type type = typeof (IExtension);
            Assert.True(target.IsImplement(type));

            type = typeof (IDisposable);
            Assert.True(target.IsImplement(type));
        }

        [Fact]
        public void IsClassTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            Assert.True(target.IsClass);

            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            Assert.False(target.IsClass);

            innerType = typeof (IDisposable);
            target = new ReflectionHelper(innerType);
            Assert.False(target.IsClass);
        }

        [Fact]
        public void IsInterfaceTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            Assert.False(target.IsInterface);

            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            Assert.False(target.IsInterface);

            innerType = typeof (IDisposable);
            target = new ReflectionHelper(innerType);
            Assert.True(target.IsInterface);
        }

        [Fact]
        public void IsObjectTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            Assert.True(target.IsObject);

            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            Assert.False(target.IsObject);

            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            Assert.False(target.IsObject);
        }

        [Fact]
        public void IsTypeTest ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            var value = new object();
            Assert.True(target.IsType(value));

            value = new string('0', 1);
            Assert.True(target.IsType(value));

            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = new ListExtension();
            Assert.True(target.IsType(value));

            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = new object();
            Assert.False(target.IsType(value));

            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = new object();
            Assert.False(target.IsType(value));

            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = new int();
            Assert.True(target.IsType(value));
        }

        [Fact]
        public void IsTypeTest1 ()
        {
            Type innerType = typeof (object);
            var target = new ReflectionHelper(innerType);
            Type value = typeof (object);
            Assert.True(target.IsType(value));

            value = typeof (string);
            Assert.True(target.IsType(value));

            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = typeof (ListExtension);
            Assert.True(target.IsType(value));

            innerType = typeof (IExtension);
            target = new ReflectionHelper(innerType);
            value = typeof (object);
            Assert.False(target.IsType(value));

            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = typeof (object);
            Assert.False(target.IsType(value));

            innerType = typeof (int);
            target = new ReflectionHelper(innerType);
            value = typeof (int);
            Assert.True(target.IsType(value));

            innerType = typeof (object);
            target = new ReflectionHelper(innerType);
            value = typeof (Type);
            Assert.True(target.IsType(value));
        }
    }
}