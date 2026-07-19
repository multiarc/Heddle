using System;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

// The D24 engine surface is driven against this assembly's own declarative export.
[assembly: ExportFunctions(typeof(Heddle.Tests.ExportedFunctions))]

namespace Heddle.Tests
{
    /// <summary>The corpus export container for <see cref="FunctionExportTests"/> (D24): a public static class
    /// whose eligible public static methods export under their lowercase-invariant names.</summary>
    public static class ExportedFunctions
    {
        public static string TitleCase(string value) => value;
        public static string TitleCase(string value, bool upper) => upper ? value.ToUpperInvariant() : value;

        // Exact-signature twin of the built-in 'upper' (string) -> string: replaces it under D12 replace rules.
        public static string Upper(string value) => "X" + value;
    }

    public static class SecondContainer
    {
        public static string TitleCase(string value) => "second";
    }

    /// <summary>
    /// The D24 engine surface: the <see cref="ExportFunctionsAttribute"/> shape and
    /// <see cref="FunctionRegistry.RegisterFrom"/> discovery — lowercase-invariant name derivation, overload
    /// grouping, replace-on-exact-signature across containers in the pinned order, freeze → throw, invalid export
    /// → <see cref="ArgumentException"/> naming the offender, idempotent re-application, built-in override.
    /// </summary>
    public class FunctionExportTests
    {
        private sealed class NotAStaticClass { public static string F(string s) => s; }
        public sealed class SealedButNotStatic { public static string F(string s) => s; }
        public static class VoidMethodContainer { public static void Bad(string s) { } public static string Ok(string s) => s; }
        public static class OpenGenericContainer { public static T Bad<T>(T x) => x; }

        [Fact]
        public void AttributeExposesContainers()
        {
            var attr = typeof(ExportedFunctions).Assembly.GetCustomAttributes<ExportFunctionsAttribute>()
                .First(a => a.Containers.Contains(typeof(ExportedFunctions)));
            Assert.Contains(typeof(ExportedFunctions), attr.Containers);
        }

        [Fact]
        public void RegisterFromDerivesLowercaseNamesAndGroupsOverloads()
        {
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(ExportedFunctions).Assembly);

            Assert.True(registry.Contains("titlecase"));      // TitleCase -> titlecase (invariant lowercase)
            Assert.False(registry.Contains("TitleCase"));      // lookup is ordinal, case-sensitive

            var overloads = registry.EnumerateOverloads().Where(o => o.Name == "titlecase").ToList();
            Assert.Equal(2, overloads.Count);                  // (string) and (string, bool) group by derived name
        }

        [Fact]
        public void RegisterContainerReplacesBuiltInOnExactSignature()
        {
            var registry = new FunctionRegistry();
            registry.RegisterContainer(typeof(ExportedFunctions));
            var upper = registry.EnumerateOverloads().Single(o => o.Name == "upper" &&
                o.ParameterTypes.Length == 1 && o.ParameterTypes[0] == typeof(string));
            Assert.Equal(typeof(ExportedFunctions), upper.Method.DeclaringType);
        }

        [Fact]
        public void ReplaceAcrossContainersFollowsPinnedOrder()
        {
            var registry = new FunctionRegistry();
            registry.RegisterContainer(typeof(ExportedFunctions));
            registry.RegisterContainer(typeof(SecondContainer)); // later container wins on the exact signature
            var single = registry.EnumerateOverloads().Single(o => o.Name == "titlecase" &&
                o.ParameterTypes.Length == 1);
            Assert.Equal(typeof(SecondContainer), single.Method.DeclaringType);
        }

        [Fact]
        public void RegisterFromIsIdempotent()
        {
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(ExportedFunctions).Assembly);
            int firstCount = registry.EnumerateOverloads().Count(o => o.Name == "titlecase");
            registry.RegisterFrom(typeof(ExportedFunctions).Assembly);
            int secondCount = registry.EnumerateOverloads().Count(o => o.Name == "titlecase");
            Assert.Equal(firstCount, secondCount);
        }

        [Fact]
        public void RegisterFromThrowsWhenFrozen()
        {
            var registry = new FunctionRegistry();
            registry.Freeze();
            Assert.Throws<InvalidOperationException>(() => registry.RegisterFrom(typeof(ExportedFunctions).Assembly));
        }

        [Fact]
        public void NonStaticContainerThrowsArgumentException()
        {
            var registry = new FunctionRegistry();
            var ex = Assert.Throws<ArgumentException>(() => registry.RegisterContainer(typeof(SealedButNotStatic)));
            Assert.Contains(nameof(SealedButNotStatic), ex.Message);
        }

        [Fact]
        public void VoidMethodInContainerThrowsArgumentExceptionNamingOffender()
        {
            var registry = new FunctionRegistry();
            var ex = Assert.Throws<ArgumentException>(() => registry.RegisterContainer(typeof(VoidMethodContainer)));
            Assert.Contains("Bad", ex.Message);
        }

        [Fact]
        public void OpenGenericMethodInContainerThrowsArgumentException()
        {
            var registry = new FunctionRegistry();
            var ex = Assert.Throws<ArgumentException>(() => registry.RegisterContainer(typeof(OpenGenericContainer)));
            Assert.Contains("Bad", ex.Message);
        }

        [Fact]
        public void RegisterFreeze() => Freeze(new FunctionRegistry());

        private static void Freeze(FunctionRegistry r)
        {
            // Guard: default registry is frozen; a fresh one is mutable until first compile use.
            Assert.False(r.IsFrozen);
        }
    }
}
