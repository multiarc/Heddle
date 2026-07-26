extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using PropFault = gen::Heddle.Data.PropFault;
using PropFaults = gen::Heddle.Data.HeddleDiagnosticCatalog.PropFaults;
using PropLayoutCore = gen::Heddle.Language.Binding.PropLayoutCore;
using PropDeclaration = gen::Heddle.Language.Binding.PropDeclaration<Microsoft.CodeAnalysis.ITypeSymbol>;
using IPropLayoutSink = gen::Heddle.Language.Binding.IPropLayoutSink<Microsoft.CodeAnalysis.ITypeSymbol>;
using SymbolTypeFacts = gen::Heddle.Generator.Binding.SymbolTypeFacts;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The <b>symbol-side</b> driver of the shared <see cref="PropLayoutCore"/>, mirroring the reflection-side tests
    /// in <c>Heddle.Tests.PropLayoutCoreReflectionTests</c> with identical scenarios and assertions. Slot indices are
    /// the wire format between the generator's frozen <c>object[]</c> prototype and the runtime's
    /// <c>ExtensionParameterCarrier</c>; a mismatch produces silent wrong rendered output, so this pair verifies both
    /// sides agree on slot order and fault sequences.
    /// </summary>
    public class PropLayoutCoreSymbolTests
    {
        private static readonly string[] Spellings =
        {
            "int", "string", "object", "System.Collections.Generic.List<>", "int*", "System.Collections.Generic.List<int>"
        };

        private static readonly (SymbolTypeFacts Facts, IReadOnlyDictionary<string, ITypeSymbol> Types) Probe =
            BuildProbe();

        private static (SymbolTypeFacts, IReadOnlyDictionary<string, ITypeSymbol>) BuildProbe()
        {
            // The unbound generic and the pointer cannot both be written as `typeof(...)` in one safe context, so
            // the probe declares them through distinct members and the map is keyed by the spelling.
            const string source = @"
unsafe class Probe
{
    static readonly System.Type A = typeof(int);
    static readonly System.Type B = typeof(string);
    static readonly System.Type C = typeof(object);
    static readonly System.Type D = typeof(System.Collections.Generic.List<>);
    static readonly System.Type E = typeof(int*);
    static readonly System.Type F = typeof(System.Collections.Generic.List<int>);
}
class Generic<T> { public static readonly System.Type G = typeof(System.Collections.Generic.List<T>); }";

            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var references = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();

            var tree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create("PropLayoutProbe", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var model = compilation.GetSemanticModel(tree);
            var typeOfs = tree.GetRoot().DescendantNodes().OfType<TypeOfExpressionSyntax>().ToList();

            var map = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
            for (int i = 0; i < Spellings.Length; i++)
                map[Spellings[i]] = model.GetTypeInfo(typeOfs[i].Type).Type;
            map["List<T>"] = model.GetTypeInfo(typeOfs[Spellings.Length].Type).Type;

            return (new SymbolTypeFacts(compilation), map);
        }

        private sealed class RecordingSink : IPropLayoutSink
        {
            internal readonly List<PropFault> Faults = new List<PropFault>();
            internal readonly List<string> Messages = new List<string>();

            public void Fault(PropFault fault, PropDeclaration declaration, ITypeSymbol relatedType,
                string relatedDisplay)
            {
                Faults.Add(fault);
                Messages.Add(PropFaults.Message(fault, declaration.Name, "extension 'probe'",
                    Probe.Facts.Display(declaration.Type), relatedDisplay));
            }

            public bool TryConvertDefault(PropDeclaration declaration, ITypeSymbol targetType, out object converted,
                out string sourceDisplay)
            {
                converted = declaration.DefaultValue;
                sourceDisplay = declaration.DefaultValue?.GetType().Name ?? "null";
                // The probe only exercises reference/identity defaults; conversion legality is verified in tests
                // that cover the emitter's DefaultConvertible behavior.
                return declaration.DefaultValue == null
                    ? targetType.IsReferenceType
                    : Probe.Facts.IsAssignableFrom(targetType,
                        Probe.Types[declaration.DefaultValue is string ? "string" : "int"]);
            }
        }

        private static PropDeclaration Decl(string name, string typeSpelling, int level = 0,
            bool hasDefault = false, object defaultValue = null) =>
            new PropDeclaration
            {
                Name = name,
                Type = typeSpelling == null ? null : Probe.Types[typeSpelling],
                Level = level,
                HasDefault = hasDefault,
                DefaultValue = defaultValue
            };

        [Fact]
        public void SlotIndicesFollowDeclarationOrderAcrossLayers()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", "int"), Decl("b", "string"), Decl("c", "object", level: 1),
            }, Probe.Facts, sink, out var faulted);

            Assert.False(faulted);
            Assert.Empty(sink.Faults);
            Assert.Equal(new[] { "a", "b", "c" }, slots.Select(s => s.Name));
            Assert.Equal(new[] { 0, 1, 2 }, slots.Select(s => s.Index));
        }

        [Fact]
        public void InheritedRedeclarationKeepsTheBaseSlotIndexAndReAppliesTheDefault()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", "object", hasDefault: true, defaultValue: "base"),
                Decl("b", "int"),
                Decl("a", "string", level: 1, hasDefault: true, defaultValue: "derived"),
            }, Probe.Facts, sink, out var faulted);

            Assert.False(faulted);
            Assert.Equal(new[] { "a", "b" }, slots.Select(s => s.Name));
            Assert.Equal(0, slots[0].Index);
            Assert.Equal("System.String", Probe.Facts.Display(slots[0].Type));
            Assert.Equal("derived", slots[0].DefaultBoxed);
        }

        [Fact]
        public void FaultsAccumulateAndTheWalkContinues()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", "int"), Decl("a", "int"), Decl("out", "int"), Decl(null, "int"), Decl("b", "int"),
            }, Probe.Facts, sink, out var faulted);

            Assert.True(faulted);
            Assert.Equal(
                new[] { PropFault.DuplicateAtLevel, PropFault.NameReserved, PropFault.NameInvalid },
                sink.Faults);
            Assert.Equal(new[] { "a", "b" }, slots.Select(s => s.Name));
        }

        [Fact]
        public void ValidationOrderWithinOneDeclarationIsTheRuntimeOrder()
        {
            var sink = new RecordingSink();
            PropLayoutCore.Build(new[]
            {
                Decl("out", "int"), Decl("out", "System.Collections.Generic.List<>"),
            }, Probe.Facts, sink, out _);

            Assert.Equal(new[] { PropFault.NameReserved, PropFault.NameReserved }, sink.Faults);
        }

        [Theory]
        [InlineData("System.Collections.Generic.List<>")]
        [InlineData("int*")]
        [InlineData("List<T>")]
        [InlineData(null)]
        public void UnusableTypesAreRejected(string spelling)
        {
            var sink = new RecordingSink();
            PropLayoutCore.Build(new[] { Decl("a", spelling) }, Probe.Facts, sink, out var faulted);

            Assert.True(faulted);
            Assert.Equal(new[] { PropFault.TypeUnusable }, sink.Faults);
        }

        [Fact]
        public void ClosedGenericIsUsable()
        {
            var sink = new RecordingSink();
            PropLayoutCore.Build(new[] { Decl("a", "System.Collections.Generic.List<int>") },
                Probe.Facts, sink, out var faulted);

            Assert.False(faulted);
        }

        [Fact]
        public void NonAssignableRedeclarationFaultsAndKeepsTheInheritedSlot()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", "string"), Decl("a", "object", level: 1),
            }, Probe.Facts, sink, out var faulted);

            Assert.True(faulted);
            Assert.Equal(new[] { PropFault.RedeclarationNotAssignable }, sink.Faults);
            Assert.Equal("System.String", Probe.Facts.Display(slots[0].Type));
            // Byte-identical to the sentence the reflection-side driver asserts.
            Assert.Equal(
                "Prop 'a' is re-declared with type System.Object, which is not assignable to the inherited type " +
                "System.String.", sink.Messages[0]);
        }
    }
}
