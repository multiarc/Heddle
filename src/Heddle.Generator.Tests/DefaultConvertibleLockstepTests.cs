extern alias gen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>Verifies emitter and runtime type conversion agree. Both tiers are driven from the same test vectors
    /// so unilateral divergence is caught immediately.</summary>
    public class DefaultConvertibleLockstepTests
    {
        private static readonly CSharpCompilation Compilation = CSharpCompilation.Create("probe",
            references: BuildProbeReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        private static MetadataReference[] BuildProbeReferences() =>
#if NETFRAMEWORK
            // mscorlib is the primitives' home on .NET Framework; there is no stand-alone System.Runtime to
            // load by simple name (the attempt is a FileNotFoundException, taking every row with it).
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
#else
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
            };
#endif

        /// <summary>Shared test vectors for both tiers.</summary>
        public static IEnumerable<object[]> Rows() => Heddle.Tests.PropDefaultConversionVectors.Rows();

        [Theory]
        [MemberData(nameof(Rows))]
        public void SymbolSideAgreesWithReflectionSide(Type source, Type target, bool expected)
        {
            // Reflection side tested elsewhere; vectors bridge the two tiers.
            Assert.Equal(expected, SymbolSide(source, target));
        }

        /// <summary>Invokes private DefaultConvertible via reflection to avoid mirroring the implementation.</summary>
        private static bool SymbolSide(Type source, Type target)
        {
            var emitterType = typeof(gen::Heddle.Generator.Emit.TemplateEmitter);
            var method = emitterType.GetMethod("DefaultConvertible",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var emitter = (gen::Heddle.Generator.Emit.TemplateEmitter) FormatterServicesCreate(emitterType);
            SetCompilationField(emitter, emitterType);

            return (bool) method.Invoke(emitter, new object[] { Symbol(source), Symbol(target) });
        }

        private static object FormatterServicesCreate(Type type) =>
#if NETFRAMEWORK
            // RuntimeHelpers.GetUninitializedObject is .NET Core-only; this is the API it wraps there.
            System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#else
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
#endif

        private static void SetCompilationField(object emitter, Type emitterType)
        {
            var field = emitterType.GetField("_compilation", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(emitter, Compilation);
        }

        private static ITypeSymbol Symbol(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                var nullable = Compilation.GetTypeByMetadataName("System.Nullable`1");
                Assert.NotNull(nullable);
                return nullable.Construct(Symbol(underlying));
            }

            var symbol = Compilation.GetTypeByMetadataName(type.FullName);
            Assert.True(symbol != null, "no symbol for " + type.FullName);
            return symbol;
        }

        /// <summary>Ensures exactly one nullable-probe method to prevent latent divergence from reappearing.</summary>
        [Fact]
        public void TheEmitterHasExactlyOneNullableUnderlyingProbe()
        {
            var emitterType = typeof(gen::Heddle.Generator.Emit.TemplateEmitter);
            var probes = emitterType
                .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name.Contains("NullableUnderlying"))
                .ToList();

            Assert.Single(probes);
            Assert.Equal("TryGetNullableUnderlying", probes[0].Name);
        }
    }
}
