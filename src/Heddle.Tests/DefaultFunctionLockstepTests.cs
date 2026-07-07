using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Models;
using Heddle.Precompiled;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 7 D21 lockstep gate: the shared-sourced <see cref="DefaultFunctionTable"/> must equal
    /// <see cref="FunctionRegistry.Default"/>'s overload set both ways, and the public
    /// <see cref="PrecompiledFunctions"/> shim must carry exactly one matching public static method per table row.
    /// "The table forgot a built-in" (or the shim did) is a red build.
    /// </summary>
    public class DefaultFunctionLockstepTests
    {
        private static string Signature(string name, IEnumerable<string> paramTypeNames, string returnTypeName)
            => $"{name}({string.Join(",", paramTypeNames)})->{returnTypeName}";

        private static Type ResolveType(string fullName)
            => Type.GetType(fullName) ?? typeof(ForModel).Assembly.GetType(fullName)
                ?? throw new InvalidOperationException($"Unresolvable type '{fullName}'.");

        private static HashSet<string> RegistrySignatures()
        {
            return FunctionRegistry.Default.EnumerateOverloads()
                .Select(o => Signature(o.Name, o.ParameterTypes.Select(t => t.FullName), o.ReturnType.FullName))
                .ToHashSet(StringComparer.Ordinal);
        }

        private static HashSet<string> TableSignatures()
        {
            return DefaultFunctionTable.Rows
                .Select(r => Signature(r.Name, r.ParameterTypeNames, r.ReturnTypeName))
                .ToHashSet(StringComparer.Ordinal);
        }

        [Fact]
        public void TableEqualsRegistryBothWays()
        {
            var registry = RegistrySignatures();
            var table = TableSignatures();

            Assert.Equal(35, DefaultFunctionTable.Rows.Count);
            Assert.Equal(35, registry.Count);
            Assert.Empty(table.Except(registry));   // no table row missing from the registry
            Assert.Empty(registry.Except(table));    // no registry overload missing from the table
        }

        [Fact]
        public void ShimHasExactlyOneMethodPerRow()
        {
            foreach (var row in DefaultFunctionTable.Rows)
            {
                var paramTypes = row.ParameterTypeNames.Select(ResolveType).ToArray();
                var method = typeof(PrecompiledFunctions).GetMethod(row.ShimMethodName,
                    BindingFlags.Public | BindingFlags.Static, null, paramTypes, null);
                Assert.True(method != null,
                    $"PrecompiledFunctions.{row.ShimMethodName}({string.Join(", ", row.ParameterTypeNames)}) is missing.");
                Assert.Equal(row.ReturnTypeName, method.ReturnType.FullName);
            }
        }

        [Fact]
        public void ShimHasNoExtraOverloads()
        {
            var shimCount = typeof(PrecompiledFunctions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Count(m => !m.IsSpecialName);
            Assert.Equal(DefaultFunctionTable.Rows.Count, shimCount);
        }

        [Fact]
        public void ShimTargetTypeNameMatchesBuiltInFunctions()
        {
            var expected = typeof(BuiltInFunctions).FullName + ", " + typeof(BuiltInFunctions).Assembly.GetName().Name;
            Assert.Equal(expected, DefaultFunctionTable.ShimTargetTypeName);
        }
    }
}
