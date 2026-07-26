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
    /// <summary>
    /// The prop-default conversion table, symbol side against reflection side. The emitter's <c>DefaultConvertible</c>
    /// is the twin of the runtime's <c>PropConversion.CanConvertTypes(source, target, allowBoxToObject: true)</c>;
    /// every rule branch gets a row here, driven through <b>both</b> implementations from one <c>(source, target)</c>
    /// pair so a unilateral edit to either is a red test naming the pair.
    /// <para>The row that motivated this work is <c>Nullable&lt;S&gt; → Nullable&lt;W&gt;</c>: the runtime had it,
    /// the emitter did not. A missing row is a safe over-<em>refusal</em> — the template falls back rather than
    /// mis-renders — but an over-refusal is still a divergence about which templates precompile.</para>
    /// <para>This row set is the seed of the assignability conformance corpus and is handed to it verbatim.</para>
    /// </summary>
    public class DefaultConvertibleLockstepTests
    {
        private static readonly CSharpCompilation Compilation = CSharpCompilation.Create("probe",
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(
                    Assembly.Load("System.Runtime").Location)
            },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        /// <summary>The shared vectors — authored in <c>Heddle.Tests</c> and linked into this project, so the
        /// two tiers are driven from one row set rather than two that agree by inspection.</summary>
        public static IEnumerable<object[]> Rows() => Heddle.Tests.PropDefaultConversionVectors.Rows();

        [Theory]
        [MemberData(nameof(Rows))]
        public void SymbolSideAgreesWithReflectionSide(Type source, Type target, bool expected)
        {
            // The reflection side of the same rows is asserted by Heddle.Tests.DefaultConvertibleReflectionTests;
            // this project cannot see PropConversion (internal to Heddle, no IVT), so the vectors are the seam.
            Assert.Equal(expected, SymbolSide(source, target));
        }

        /// <summary>
        /// Drives the emitter's own <c>DefaultConvertible</c> over the symbol pair. It is private, so it is reached
        /// by reflection rather than mirrored here — mirroring it would make this a test of the mirror.
        /// </summary>
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
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);

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

        /// <summary>The emitter's nullable probe is one method now — the file used to carry two, one over
        /// <c>ConstructedFrom</c> and one over <c>OriginalDefinition</c>, which is a latent divergence inside a
        /// single file. Asserted structurally so a third cannot quietly reappear.</summary>
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
