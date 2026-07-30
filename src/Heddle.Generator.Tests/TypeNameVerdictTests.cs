using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Every verdict the type-nameability walk can reach, asked of a constructed symbol rather than of a template.
    /// <para>Most of these have no template that reaches them: no <c>@model</c> spelling names an anonymous type, an
    /// unbound generic, a bare type parameter or a script submission, and no C# signature declares a property of an
    /// array of a ref struct. Asking through templates alone left most of the walk asserted by nothing — arms could
    /// be deleted one at a time with the whole suite green — and an arm nothing asserts is an arm that gets deleted
    /// by the next person simplifying, or quietly stops working. Each row here names one refusal and fails if that
    /// refusal goes away.</para>
    /// <para>The reachable-from-a-template half stays where it belongs, in the integration suite's hostile-model
    /// table, which also proves the consumer's build survives. This is the completeness half.</para>
    /// </summary>
    public class TypeNameVerdictTests
    {
        private static readonly IReadOnlyList<MetadataReference> Framework = BuildFramework();

        private static IReadOnlyList<MetadataReference> BuildFramework()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            return tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
        }

        private const string Source = @"
using System;
namespace Verdict
{
    public sealed class Holder<T>
    {
        public T Value => default(T);
    }

    public unsafe sealed class Unsafe
    {
        public delegate*<void> Fn => null;
    }

    public sealed class Anon
    {
        public object Make() => new { A = 1 };
    }

    /// <summary>A generic whose nested type carries no type argument of its own, so a spelling of the nested type
    /// is the only place the outer one's argument can be seen.</summary>
    public class Outer<T>
    {
        public struct Inner { public int Amount { get; set; } }
    }

    [Obsolete(""gone"", true)]
    public struct Legacy { public int Amount { get; set; } }
}";

        private static CSharpCompilation Compile() =>
            CSharpCompilation.Create("Verdict.Consumer", new[] { CSharpSyntaxTree.ParseText(Source) }, Framework,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        private static ITypeSymbol Named(Compilation compilation, string metadataName)
        {
            var type = compilation.GetTypeByMetadataName(metadataName);
            Assert.NotNull(type);
            return type;
        }

        /// <summary>The <c>delegate*&lt;void&gt;</c> property's type, which has no factory on
        /// <see cref="Compilation"/> and can only be read off a declaration.</summary>
        private static ITypeSymbol FunctionPointer(Compilation compilation) =>
            ((IPropertySymbol) Named(compilation, "Verdict.Unsafe").GetMembers("Fn").Single()).Type;

        private static ITypeSymbol TypeParameter(Compilation compilation) =>
            ((INamedTypeSymbol) Named(compilation, "Verdict.Holder`1")).TypeParameters[0];

        /// <summary>A <c>Submission</c>-kind type, which only a script compilation produces.</summary>
        private static ITypeSymbol Submission()
        {
            var script = CSharpCompilation.CreateScriptCompilation("Verdict.Script",
                CSharpSyntaxTree.ParseText("1 + 1", new CSharpParseOptions(kind: SourceCodeKind.Script)), Framework);
            var submission = script.ScriptClass;
            Assert.NotNull(submission);
            Assert.Equal(TypeKind.Submission, submission.TypeKind);
            return submission;
        }

        private static ITypeSymbol Anonymous(Compilation compilation) =>
            compilation.CreateAnonymousTypeSymbol(
                System.Collections.Immutable.ImmutableArray.Create(
                    (ITypeSymbol) compilation.GetSpecialType(SpecialType.System_Int32)),
                System.Collections.Immutable.ImmutableArray.Create("A"));

        /// <summary>
        /// Each refusal by the symbol that reaches it, paired with the words its reason has to carry.
        /// <para>The pairing is the whole point. Pinning the verdict alone let one arm answer another's rows the
        /// moment that arm was deleted, so the suite stayed green over a walk that had stopped doing what it says —
        /// and mutation testing then reported the deleted arm as dead code. It also hid a row asserting the wrong
        /// thing: <c>array-of-ref-struct</c> named <c>Span&lt;T&gt;</c>, the open definition, and was refused for
        /// being an open generic without the ref-struct rule ever being consulted.</para>
        /// </summary>
        public static TheoryData<string, string> RefusedNames() =>
            new TheoryData<string, string>
            {
                { "error", "does not resolve to a type" },
                { "pointer", "is a pointer type" },
                { "function-pointer", "is a pointer type" },
                { "type-parameter", "is a type parameter" },
                { "submission", "is not a type C# has a syntax for" },
                { "void", "is 'void'" },
                { "static", "is a static type" },
                { "anonymous", "is an anonymous type" },
                // Not the type-parameter arm: Roslyn gives an unbound generic error-typed arguments.
                { "unbound-generic", "does not resolve to a type" },
                { "open-generic", "is an open generic type" },
                { "nested-in-open-generic", "is an open generic type" },
                { "array-of-ref-struct", "is a ref struct" },
                { "array-of-pointer", "is a pointer type" },
                { "type-argument-of-type-parameter", "is a type parameter" },
                { "containing-type-argument", "is a pointer type" },
                { "array-of-type-parameter-in-containing-type", "is a type parameter" },
            };

        private static ITypeSymbol Subject(string name, Compilation compilation)
        {
            var intType = compilation.GetSpecialType(SpecialType.System_Int32);
            var listDefinition = (INamedTypeSymbol) Named(compilation, "System.Collections.Generic.List`1");
            switch (name)
            {
                case "error": return compilation.CreateErrorTypeSymbol(null, "Missing", 0);
                case "pointer": return compilation.CreatePointerTypeSymbol(intType);
                case "function-pointer": return FunctionPointer(compilation);
                case "type-parameter": return TypeParameter(compilation);
                case "submission": return Submission();
                case "void": return compilation.GetSpecialType(SpecialType.System_Void);
                case "static": return Named(compilation, "System.Math");
                case "anonymous": return Anonymous(compilation);
                case "unbound-generic": return listDefinition.ConstructUnboundGenericType();
                case "open-generic": return listDefinition;
                case "nested-in-open-generic":
                    return listDefinition.GetTypeMembers("Enumerator").Single();
                case "array-of-ref-struct":
                    return compilation.CreateArrayTypeSymbol(Span(compilation));
                case "array-of-pointer":
                    return compilation.CreateArrayTypeSymbol(compilation.CreatePointerTypeSymbol(intType));
                case "type-argument-of-type-parameter":
                    // `List<T[]>`: the type parameter is inside a type argument, inside an array.
                    return listDefinition.Construct(
                        compilation.CreateArrayTypeSymbol(TypeParameter(compilation)));
                case "containing-type-argument":
                    // `Outer<int*>.Inner`: the only type argument in the spelling belongs to the enclosing type.
                    return Nested(compilation, compilation.CreatePointerTypeSymbol(intType));
                case "array-of-type-parameter-in-containing-type":
                    // `Outer<T[]>.Inner`: an enclosing argument that is not itself the parameter but contains one.
                    return Nested(compilation, compilation.CreateArrayTypeSymbol(TypeParameter(compilation)));
                default: throw new ArgumentOutOfRangeException(nameof(name), name);
            }
        }

        /// <summary><c>Verdict.Outer&lt;<paramref name="argument"/>&gt;.Inner</c>.</summary>
        private static ITypeSymbol Nested(Compilation compilation, ITypeSymbol argument) =>
            ((INamedTypeSymbol) Named(compilation, "Verdict.Outer`1")).Construct(argument)
            .GetTypeMembers("Inner").Single();

        private static ITypeSymbol Span(Compilation compilation) =>
            ((INamedTypeSymbol) Named(compilation, "System.Span`1"))
            .Construct(compilation.GetSpecialType(SpecialType.System_Char));

        /// <summary>
        /// Every one of them is <see cref="SymbolTypeResolver.NameFault.Unusable"/>, the verdict with no author-
        /// facing warning behind it, and every one carries a reason naming the type — the emitter puts that reason
        /// in the build log when a template degrades, so an empty one is a degrade nobody can explain.
        /// <para>Asked through <c>ClassifyTypeName</c>, the lenient of the two entries: a row that only
        /// <c>ClassifyModelType</c> catches would be asserting the ref-struct rule instead of its own.</para>
        /// </summary>
        [Theory]
        [MemberData(nameof(RefusedNames))]
        public void ANameGeneratedCodeCannotWriteIsRefusedWithoutADiagnosticToShowForIt(string name, string because)
        {
            var compilation = Compile();
            var resolver = new SymbolTypeResolver(compilation);
            var subject = Subject(name, compilation);

            Assert.Equal(SymbolTypeResolver.NameFault.Unusable, resolver.ClassifyTypeName(subject, out var reason));
            Assert.Contains(because, reason ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// The other verdict a type argument of an <b>enclosing</b> type can carry, and the one a C# author can
        /// actually declare: <c>Outer&lt;Legacy&gt;.Inner</c> where <c>Legacy</c> is error-obsolete. Nothing about
        /// the nested type says so — it has no type argument of its own — and asking only its own arguments called
        /// the name writable, so the emitter spelled it and the consumer's build died on CS0619 off a property that
        /// carries no attribute at all.
        /// <para>Author-fixable in principle, hence <see cref="SymbolTypeResolver.NameFault.Unnameable"/> and not the
        /// silent verdict, and the reason names the argument rather than the type that was asked about.</para>
        /// </summary>
        [Fact]
        public void AnErrorObsoleteArgumentOfAnEnclosingTypeIsRefusedOnTheEnclosingSpelling()
        {
            var compilation = Compile();
            var resolver = new SymbolTypeResolver(compilation);
            var subject = Nested(compilation, Named(compilation, "Verdict.Legacy"));

            Assert.Equal(SymbolTypeResolver.NameFault.Unnameable, resolver.ClassifyTypeName(subject, out var reason));
            Assert.Contains("Verdict.Legacy", reason ?? string.Empty, StringComparison.Ordinal);

            // The cost control that keeps this a rule about the argument: the same nesting over an ordinary one
            // stays writable, so the walk is not a refusal of nested generics.
            Assert.Equal(SymbolTypeResolver.NameFault.None,
                resolver.ClassifyTypeName(Nested(compilation, compilation.GetSpecialType(SpecialType.System_Int32)),
                    out _));
        }

        /// <summary>
        /// The one verdict that depends on <b>where</b> the name is written. A ref struct is perfectly writable as a
        /// local, which is what keeps a hop through <c>Span&lt;char&gt;</c> to a member of its own on the precompiled
        /// tier; it may not be boxed into a model, held in an array element (CS0611) or substituted for a type
        /// argument (CS9244). Collapsing the two entries into one would cost the hop or admit the other three.
        /// </summary>
        [Fact]
        public void ARefStructIsWritableOnItsOwnAndRefusedInEveryNestedPosition()
        {
            var compilation = Compile();
            var resolver = new SymbolTypeResolver(compilation);
            var span = Named(compilation, "System.Span`1")
                .OriginalDefinition is INamedTypeSymbol definition
                ? definition.Construct(compilation.GetSpecialType(SpecialType.System_Char))
                : null;
            Assert.NotNull(span);

            Assert.Equal(SymbolTypeResolver.NameFault.None, resolver.ClassifyTypeName(span, out _));
            Assert.Equal(SymbolTypeResolver.NameFault.Unusable, resolver.ClassifyModelType(span, out _));
            Assert.Equal(SymbolTypeResolver.NameFault.Unusable,
                resolver.ClassifyTypeName(compilation.CreateArrayTypeSymbol(span), out _));
            Assert.Equal(SymbolTypeResolver.NameFault.Unusable, resolver.ClassifyTypeName(
                ((INamedTypeSymbol) Named(compilation, "System.Collections.Generic.List`1")).Construct(span), out _));
        }

        /// <summary>The cost control over the whole table: the ordinary spellings a real model is made of stay
        /// nameable in both entries, nested ones included, so the walk is a filter and not a refusal of structure.
        /// </summary>
        [Theory]
        [InlineData("System.String")]
        [InlineData("System.Collections.Generic.List`1")]
        [InlineData("System.Collections.Generic.Dictionary`2")]
        public void AnOrdinaryConstructedTypeStaysNameableInEveryPosition(string metadataName)
        {
            var compilation = Compile();
            var resolver = new SymbolTypeResolver(compilation);
            var intType = compilation.GetSpecialType(SpecialType.System_Int32);
            var definition = (INamedTypeSymbol) Named(compilation, metadataName);
            var subject = definition.Arity == 0
                ? definition
                : definition.Construct(Enumerable.Repeat((ITypeSymbol) intType, definition.Arity).ToArray());

            Assert.Equal(SymbolTypeResolver.NameFault.None, resolver.ClassifyTypeName(subject, out _));
            Assert.Equal(SymbolTypeResolver.NameFault.None, resolver.ClassifyModelType(subject, out _));
            Assert.Equal(SymbolTypeResolver.NameFault.None,
                resolver.ClassifyModelType(compilation.CreateArrayTypeSymbol(subject), out _));
        }
    }
}
