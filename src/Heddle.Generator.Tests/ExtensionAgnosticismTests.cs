extern alias gen;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <b>The architectural gate.</b> The generator is a compiler for the Heddle language, not a catalogue of the
    /// extensions that ship with it. Every extension name it hardcodes is a third-party extension it silently
    /// cannot serve — that is what the private four-name step-back list was, and five of the nine extensions
    /// sharing one hook body were missing from it for no reason but its length.
    /// <para>The rule is stated over <b>what is compiled into <c>Heddle.Generator.dll</c></b>, which is not the
    /// generator's own directory: <c>Heddle.Generator.Common.props</c> links the whole of
    /// <c>src/Heddle/Language/</c> and twenty-odd more files into the assembly, so a directory walk sees about a
    /// third of it and misses the largest name table in it. The compile-input manifest that props file embeds is
    /// the file set this gate reads; the directory walk survives only as a lower bound on it.</para>
    /// <para>What is collected is <b>vocabulary-free</b>: every identifier-shaped string literal in a
    /// <i>name-ish position</i> — an <c>==</c>/<c>!=</c> operand, a <c>case</c> label or constant pattern, an
    /// argument to <c>Equals</c>/<c>Contains</c>/<c>TryGetValue</c>/<c>StartsWith</c> and their neighbours, an
    /// element of a collection or object initializer, an element-access key, a <c>const string</c>, or a default
    /// parameter value. It does not need to know what an extension name is, which is the point: the previous gate
    /// reflected the engine's own <c>[ExtensionName]</c> list, so hardcoding a THIRD-PARTY name passed every
    /// assertion it made. It also could not see <c>string.Equals(assemblyName, "Heddle", Ordinal)</c> — it looked
    /// for <c>==</c> — and so was green while the code did exactly what it claims to ban.</para>
    /// <para>Each row of the ledger carries a <c>Why</c> that ends either in <c>permanent</c> or in the stage that
    /// retires it, and the set is compared with set equality, so the list can only grow by writing down what was
    /// added and why. It is longer than the list it replaces because it inspects the whole assembly rather than a
    /// third of it, and by every form a name can be written in rather than one.</para>
    /// <para><b>What it does not claim.</b> A literal that is not identifier-shaped is not a name — that is what
    /// keeps two hundred diagnostic messages out of the ledger — so a hardcoded <i>document</i>, such as a
    /// synthesized template, is invisible here and answered by deleting the document instead. A dotted type name
    /// is answered by <see cref="TheGeneratorSpellsNoEngineExtensionTypeItCouldBind"/>, and a table whose keys are
    /// built at runtime is answered by <see cref="NoStaticStringTableIsKeyedByAnExtensionName"/>, which reads
    /// values rather than source text.</para>
    /// </summary>
    public class ExtensionAgnosticismTests
    {
        /// <summary>The name-ish literals the generator's compile inputs are allowed to carry, by
        /// <c>(file, value, syntactic form, count)</c>. The form is part of the key on purpose: rewriting
        /// <c>string.Equals(x, "Heddle", Ordinal)</c> as <c>x == "Heddle"</c> is a different row, not the same
        /// one.</summary>
        private static readonly (string File, string Value, string Form, int Count, string Why)[] AllowedNameLiterals =
        {
            // src/Heddle.Generator/Binding/CSharpExpressionTyper.cs
            ("src/Heddle.Generator/Binding/CSharpExpressionTyper.cs", "CS0656", "equality", 1,
                "a Roslyn diagnostic ID: the consumer cannot bind dynamic at all; permanent"),
            ("src/Heddle.Generator/Binding/CSharpExpressionTyper.cs", "CS1969", "equality", 1,
                "a Roslyn diagnostic ID: the consumer cannot bind dynamic at all; permanent"),

            // src/Heddle.Generator/Binding/EmbeddedCSharpFragment.cs
            ("src/Heddle.Generator/Binding/EmbeddedCSharpFragment.cs", "CSharpExpression", "const-field", 1,
                "the preparse template's own class name, spelled once; permanent"),
            ("src/Heddle.Generator/Binding/EmbeddedCSharpFragment.cs", "PreProcessData", "const-field", 1,
                "the preparse template's own method name, spelled once; permanent"),
            ("src/Heddle.Generator/Binding/EmbeddedCSharpFragment.cs", "System", "call:Add", 1,
                "the namespace every embedded expression is given; permanent"),

            // src/Heddle.Generator/Binding/FunctionExportResolver.cs
            ("src/Heddle.Generator/Binding/FunctionExportResolver.cs", "SpecialNameAttribute", "equality", 1,
                "a BCL attribute's metadata name; permanent"),

            // src/Heddle.Generator/Binding/SymbolTypeResolver.cs
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "ArgIterator", "equality", 1,
                "a by-ref-like BCL type no expression may name; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "ObsoleteAttribute", "call:Equals", 1,
                "a BCL attribute's metadata name; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "RuntimeArgumentHandle", "equality", 1,
                "a by-ref-like BCL type no expression may name; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "System", "equality", 2,
                "the namespace of the special types resolved by name; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "TypedReference", "equality", 1,
                "a by-ref-like BCL type no expression may name; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "bool", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "byte", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "char", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "decimal", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "double", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "dynamic", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "float", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "int", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "long", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "object", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "sbyte", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "short", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "string", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "uint", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "ulong", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),
            ("src/Heddle.Generator/Binding/SymbolTypeResolver.cs", "ushort", "element-access", 1,
                "a C# type keyword mapped to its special type; permanent"),

            // src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs
            ("src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs", "HeddleRefusalCategory", "const-field", 1,
                "the diagnostic-property key HED7031 carries its category under; permanent"),

            // src/Heddle.Generator/Emit/ExtensionBinder.cs
            ("src/Heddle.Generator/Emit/ExtensionBinder.cs", "CompleteInit", "equality", 1,
                "AbstractExtension's compile-time hook name, read off the base type; permanent"),
            ("src/Heddle.Generator/Emit/ExtensionBinder.cs", "Default", "equality", 1,
                "a [Prop] named argument; permanent"),
            ("src/Heddle.Generator/Emit/ExtensionBinder.cs", "InitStart", "equality", 1,
                "AbstractExtension's compile-time hook name, read off the base type; permanent"),
            ("src/Heddle.Generator/Emit/ExtensionBinder.cs", "Optional", "equality", 1,
                "a [Prop] named argument; permanent"),

            // src/Heddle.Generator/Emit/TemplateEmitter.cs
            ("src/Heddle.Generator/Emit/TemplateEmitter.cs", "dynamic", "call:Equals", 3,
                "a C# type keyword the emitter spells; permanent"),
            ("src/Heddle.Generator/Emit/TemplateEmitter.cs", "object", "call:Equals", 1,
                "a C# type keyword the emitter spells; permanent"),
            ("src/Heddle.Generator/Emit/TemplateEmitter.cs", "out", "equality", 1,
                "@out has its own projection emission rather than an Init; retires in Stage 6"),
            ("src/Heddle.Generator/Emit/TemplateEmitter.cs", "partial", "equality", 1,
                "@partial resolves a template key rather than binding; retires in Stage 6"),

            // src/Heddle/Data/HeddleDiagnosticCatalog.cs
            ("src/Heddle/Data/HeddleDiagnosticCatalog.cs", "out", "initializer", 1,
                "a call-shape keyword no [Prop] name may take; grammar keyword, permanent"),
            ("src/Heddle/Data/HeddleDiagnosticCatalog.cs", "this", "initializer", 1,
                "the chained value's keyword, reserved as a [Prop] name; grammar keyword, permanent"),

            // src/Heddle/Data/OutputProfileRules.cs
            ("src/Heddle/Data/OutputProfileRules.cs", "html", "const-field", 1,
                "an @profile spelling the parser accepts; grammar keyword, permanent"),
            ("src/Heddle/Data/OutputProfileRules.cs", "text", "const-field", 1,
                "an @profile spelling the parser accepts; grammar keyword, permanent"),

            // src/Heddle/Helpers/CSharpTypeNames.cs
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "bool", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "byte", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "char", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "decimal", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "double", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "dynamic", "const-field", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "float", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "int", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "long", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "object", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "sbyte", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "short", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "string", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "uint", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "ulong", "initializer", 1,
                "a C# type keyword; permanent"),
            ("src/Heddle/Helpers/CSharpTypeNames.cs", "ushort", "initializer", 1,
                "a C# type keyword; permanent"),

            // src/Heddle/Language/DirectiveNames.cs
            ("src/Heddle/Language/DirectiveNames.cs", "model", "const-field", 1,
                "the @model directive, read at parse level before anything can be bound; grammar keyword, permanent"),
            ("src/Heddle/Language/DirectiveNames.cs", "profile", "const-field", 1,
                "the @profile directive, read at parse level to know the running profile; grammar keyword, permanent"),
            ("src/Heddle/Language/DirectiveNames.cs", "using", "const-field", 1,
                "the @using directive, read at parse level before any type spelling resolves; grammar keyword, permanent"),

            // src/Heddle/Language/Binding/UsingDirectives.cs
            ("src/Heddle/Language/Binding/UsingDirectives.cs", "static", "const-field", 1,
                "C#'s own `using static` modifier; grammar keyword, permanent"),

            // src/Heddle/Language/BodyModelRules.cs
            ("src/Heddle/Language/BodyModelRules.cs", "attr", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "date", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "elif", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "else", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "elseif", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "for", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "guid", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "if", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "ifnot", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "int", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "js", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "list", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "money", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "string", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "time", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),
            ("src/Heddle/Language/BodyModelRules.cs", "url", "element-access", 1,
                "a key of the pinned body-typing table; retires in Stage 6"),

            // src/Heddle/Language/Expressions/EmbeddedCSharpNames.cs
            ("src/Heddle/Language/Expressions/EmbeddedCSharpNames.cs", "chained", "const-field", 1,
                "an identifier an embedded C# expression may bind; grammar keyword, permanent"),
            ("src/Heddle/Language/Expressions/EmbeddedCSharpNames.cs", "model", "const-field", 1,
                "an identifier an embedded C# expression may bind; grammar keyword, permanent"),
            ("src/Heddle/Language/Expressions/EmbeddedCSharpNames.cs", "root", "const-field", 1,
                "an identifier an embedded C# expression may bind; grammar keyword, permanent"),

            // src/Heddle/Language/HeddleMainListener.cs
            ("src/Heddle/Language/HeddleMainListener.cs", "import", "equality", 1,
                "the @<< import directive named in the parse listener; grammar keyword, permanent"),

            // src/Heddle/Language/OutputLints.cs
            ("src/Heddle/Language/OutputLints.cs", "action", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "background", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "cite", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "data", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "formaction", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "href", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "longdesc", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "manifest", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "poster", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "src", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "srcset", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),
            ("src/Heddle/Language/OutputLints.cs", "usemap", "initializer", 1,
                "an HTML attribute whose value is a URL; permanent"),

            // src/Heddle/Language/ParseContext.cs
            ("src/Heddle/Language/ParseContext.cs", "out", "call:Equals", 2,
                "the slot parameter keyword in a def header; grammar keyword, permanent"),

            // src/Heddle/Precompiled/HeddleBuildOptions.cs
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleEmitUtf8Pieces", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent"),
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleExpressionMode", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent"),
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleGeneratedNamespace", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent"),
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleMaxRecursionCount", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent"),
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleNodeFallback", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent"),
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleOutputProfile", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent"),
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleTemplateRoot", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent"),
            ("src/Heddle/Precompiled/HeddleBuildOptions.cs", "HeddleTrimDirectiveLines", "const-field", 1,
                "an MSBuild property or metadata name the build options read; permanent")
        };

        /// <summary>Files whose name-ish literals are all of one shape, gated by that shape rather than row by
        /// row. One entry: the diagnostic-ID table, whose ninety-four <c>HED####</c> constants are already gated
        /// by ID against the registry, and whose rows here would say nothing and bury the ledger. Anything in that
        /// file that is NOT of the declared shape still needs a row above.</summary>
        private static readonly (string File, string Shape, string Why)[] DeclaredLiteralShapes =
        {
            ("src/Heddle/Data/HeddleDiagnosticIds.cs", @"^HED[0-9]{4}$",
                "the diagnostic-ID registry's own IDs, gated by ID by DiagnosticIdTests; permanent")
        };

        /// <summary>The engine-extension type references the compile inputs are allowed to spell, by
        /// <c>(file, reference, count)</c>. The namespaces searched are computed off the engine assembly, so a
        /// built-in moved to a new namespace stays covered.</summary>
        private static readonly (string File, string Reference, int Count, string Why)[]
            AllowedEngineExtensionReferences =
        {
            // Empty, and asserted so: the emitter binds every extension it emits — the unnamed carrier through the
            // registry name the shared profile rule computes, @out through the name it is called by — so there is
            // no engine extension left whose type it spells.

        };

        /// <summary>The static string-keyed tables in the built assembly that are allowed to contain a built-in
        /// extension name, by <c>(declaring type, member, the intersecting names)</c>. Keyed on the VALUES read out
        /// of the built assembly, so renaming or moving a table changes nothing here — the two that would matter,
        /// <c>BodyModelRules.Table</c> and its <c>PinnedNames</c> projection, are named with the stage that
        /// deletes them. The rest collide by coincidence: the engine registers encoders called <c>@int</c> and
        /// <c>@string</c>, so every C# keyword table intersects the extension names by two.</summary>
        private static readonly (string Type, string Member, string Names, string Why)[] AllowedNameKeyedTables =
        {
            ("Heddle.Data.HeddleDiagnosticCatalog+PropFaults", "ReservedNames", "out",
                "the call-shape keywords no [Prop] name may take; grammar keyword, permanent"),
            ("Heddle.Generator.Binding.SymbolTypeResolver", "Keywords", "int, string",
                "the C# keyword to special-type map; permanent"),
            ("Heddle.Helpers.CSharpTypeNames", "AliasNames", "int, string", "C# type keywords; permanent"),
            ("Heddle.Helpers.CSharpTypeNames", "Aliases", "int, string", "C# type keywords; permanent"),
            ("Heddle.Helpers.CSharpTypeNames", "AliasToType", "int, string", "C# type keywords; permanent"),
            ("Heddle.Helpers.CSharpTypeNames", "Names", "int, string", "C# type keywords; permanent"),
            ("Heddle.Language.BodyModelRules", "PinnedNames",
                "attr, date, elif, else, elseif, for, guid, if, ifnot, int, js, list, money, string, time, url",
                "the pinned body-typing table, projected; retires in Stage 6"),
            ("Heddle.Language.BodyModelRules", "Table",
                "attr, date, elif, else, elseif, for, guid, if, ifnot, int, js, list, money, string, time, url",
                "the pinned body-typing table itself; retires in Stage 6")
        };

        /// <summary>Types whose statics cannot be read — reading one runs its type initializer, and a generator
        /// type can need a host the test process is not. Empty today, and it is a declared list rather than a
        /// <c>catch { continue; }</c> so that it stays empty visibly: a type that starts throwing reddens this
        /// gate instead of quietly leaving its tables uninspected.</summary>
        private static readonly (string Type, string Why)[] UninspectableStatics = { };

        /// <summary>The manifest <c>Heddle.Generator.Common.props</c> writes and embeds: every file the compiler
        /// was given, repository-relative, one per line.</summary>
        private const string ManifestResourceName = "Heddle.Generator.CompileInputs.txt";

        private static Assembly GeneratorAssembly =>
            typeof(gen::Heddle.Generator.Emit.ExtensionBinder).Assembly;

        /// <summary>The method names whose string arguments name something: a literal handed to one of these is
        /// being compared or looked up, whatever the surrounding expression looks like.</summary>
        private static readonly string[] NameishMethods =
        {
            "Equals", "Compare", "CompareOrdinal", "StartsWith", "EndsWith", "Contains", "IndexOf",
            "TryGetValue", "ContainsKey", "Add", "Remove", "TryResolve", "TryGet"
        };

        private static readonly Lazy<IReadOnlyList<(string Path, SyntaxTree Tree)>> ParsedInputs =
            new Lazy<IReadOnlyList<(string Path, SyntaxTree Tree)>>(ParseCompileInputs);

        /// <summary>The manifest is the file set every other assertion here reads, so it is asserted first and on
        /// its own: that it exists, that it is the size of an assembly rather than of an accident, that every path
        /// in it is on disk, and that it covers everything the directory walk it replaces used to cover. A target
        /// that silently stopped running must redden this rather than make the gates below vacuous.</summary>
        [Fact]
        public void TheCompileInputManifestNamesEveryFileCompiledIntoTheGenerator()
        {
            var root = RepoRoot();
            var paths = ManifestPaths();

            Assert.True(paths.Count >= 100,
                "The compile-input manifest lists only " + paths.Count + " files. Heddle.Generator.dll is built " +
                "from its own directory plus the linked shared sources, so a list this short means the " +
                "HeddleWriteCompileManifest target in src/Heddle.Generator/Heddle.Generator.Common.props stopped " +
                "seeing @(Compile) — every assertion in this class would then pass by inspecting nothing.");

            foreach (var relative in paths)
            {
                Assert.False(Path.IsPathRooted(relative) || relative.Contains("\\"),
                    "The manifest entry '" + relative + "' is not a repository-relative path with '/' separators.");
                Assert.True(File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))),
                    "The manifest names '" + relative + "', which is not on disk under " + root + ".");
            }

            var walk = GeneratorDirectorySources(root);
            var unseen = walk.Except(paths, StringComparer.Ordinal).ToList();
            Assert.True(unseen.Count == 0,
                "The manifest does not cover every .cs file under src/Heddle.Generator, so it is not a superset of " +
                "the directory walk it replaces. Missing: " + string.Join(", ", unseen));
            Assert.True(paths.Count > walk.Count,
                "The manifest lists no more than the generator's own directory (" + paths.Count + " vs " +
                walk.Count + "). The linked shared sources are compiled into the assembly and must be in it.");
        }

        /// <summary>No name hardcoded anywhere in the assembly outside the declared ledger. This is the assertion
        /// the three the class used to make were reaching for: it is blind to what a built-in extension is called,
        /// so a third-party name is caught on the same terms, and it keys on the syntactic form, so
        /// <c>==</c>, <c>string.Equals</c>, a <c>case</c> label, a dictionary key and a default parameter value are
        /// each a row rather than four ways to evade one regex.</summary>
        [Fact]
        public void TheGeneratorHardcodesNoNameOutsideTheDeclaredLedger()
        {
            var shapes = DeclaredLiteralShapes.ToDictionary(
                s => s.File, s => new Regex(s.Shape), StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (path, tree) in ParsedInputs.Value)
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var literal = node as LiteralExpressionSyntax;
                if (literal == null || !literal.IsKind(SyntaxKind.StringLiteralExpression))
                    continue;
                var value = literal.Token.Value as string;
                if (value == null || !IsIdentifierShaped(value))
                    continue;
                var form = NameishForm(literal);
                if (form == null)
                    continue;
                Regex shape;
                if (shapes.TryGetValue(path, out shape) && shape.IsMatch(value))
                    continue;

                var key = path + " " + value + " " + form;
                int seen;
                counts[key] = counts.TryGetValue(key, out seen) ? seen + 1 : 1;
            }

            var observed = new SortedSet<string>(counts.Select(c => c.Key + " x" + c.Value), StringComparer.Ordinal);
            var declared = new SortedSet<string>(
                AllowedNameLiterals.Select(a => a.File + " " + a.Value + " " + a.Form + " x" + a.Count),
                StringComparer.Ordinal);

            var actual = WriteActual("name-literals", observed);
            Assert.True(declared.SetEquals(observed),
                Describe("The name-ish string literals compiled into Heddle.Generator.dll",
                    declared, observed, actual));
            AssertWhysNameTheirFate(AllowedNameLiterals.Select(a => (a.File + " " + a.Value, a.Why)));
            AssertWhysNameTheirFate(DeclaredLiteralShapes.Select(s => (s.File + " " + s.Shape, s.Why)));
        }

        /// <summary>No engine-extension type spelled outside the allowlist. The namespaces searched are
        /// <b>computed</b> — every namespace the engine assembly declares an <c>[ExtensionName]</c> type in — so a
        /// built-in that moves namespace is still covered, which the <c>Heddle\.Extensions\.</c> regex this
        /// replaces was not. The emitter names the type it binds through the binder's
        /// <c>GlobalName</c>/<c>BareTypeName</c>, so a subclass, a nested type or a replacement registered by a
        /// package is spelled correctly without the emitter knowing it exists.</summary>
        [Fact]
        public void TheGeneratorSpellsNoEngineExtensionTypeItCouldBind()
        {
            var namespaces = ExtensionNamespaces();
            Assert.True(namespaces.Count != 0,
                "No type in " + typeof(global::Heddle.HeddleTemplate).Assembly.GetName().Name +
                " carries [ExtensionName], so this gate has nothing to search for and would pass vacuously.");

            var patterns = namespaces
                .Select(ns => new Regex(@"(?<![\w.])" + Regex.Escape(ns) + @"(\.[A-Za-z_][A-Za-z0-9_]*)?"))
                .ToList();

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (path, tree) in ParsedInputs.Value)
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                string text = null;
                var literal = node as LiteralExpressionSyntax;
                if (literal != null && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    text = literal.Token.Value as string;
                else if (IsMaximalDottedName(node))
                    text = node.ToString();
                if (text == null)
                    continue;

                foreach (var pattern in patterns)
                foreach (Match match in pattern.Matches(text))
                {
                    var key = path + " " + match.Value;
                    int seen;
                    counts[key] = counts.TryGetValue(key, out seen) ? seen + 1 : 1;
                }
            }

            var observed = new SortedSet<string>(counts.Select(c => c.Key + " x" + c.Value), StringComparer.Ordinal);
            var declared = new SortedSet<string>(
                AllowedEngineExtensionReferences.Select(a => a.File + " " + a.Reference + " x" + a.Count),
                StringComparer.Ordinal);

            var actual = WriteActual("engine-extension-references", observed);
            Assert.True(declared.SetEquals(observed),
                Describe("The engine-extension types spelled in Heddle.Generator.dll's sources",
                    declared, observed, actual));
            AssertWhysNameTheirFate(AllowedEngineExtensionReferences.Select(a => (a.File + " " + a.Reference, a.Why)));
        }

        /// <summary>No static string-keyed table in the built assembly is keyed by an extension name — asserted by
        /// reading the VALUES out of <c>Heddle.Generator.dll</c>, not by looking at source text. A name table
        /// renamed, moved to another file or built out of constants is the same table by this measure, and the
        /// linked shared types are covered because they are types in this assembly like any other.</summary>
        [Fact]
        public void NoStaticStringTableIsKeyedByAnExtensionName()
        {
            var names = new HashSet<string>(BuiltInNames(), StringComparer.Ordinal);
            var skipped = new HashSet<string>(UninspectableStatics.Select(u => u.Type), StringComparer.Ordinal);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            var observed = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in LoadableTypes(GeneratorAssembly))
            {
                if (type.ContainsGenericParameters || type.FullName == null)
                    continue;

                var members = type.GetFields(flags).Cast<MemberInfo>()
                    .Concat(type.GetProperties(flags).Where(p => p.GetMethod != null &&
                                                                 p.GetIndexParameters().Length == 0));
                foreach (var member in members)
                {
                    var field = member as FieldInfo;
                    var property = member as PropertyInfo;
                    if (!IsStringKeyed(field != null ? field.FieldType : property.PropertyType))
                        continue;

                    object value;
                    try
                    {
                        value = field != null ? field.GetValue(null) : property.GetValue(null);
                    }
                    catch (Exception e)
                    {
                        Assert.True(skipped.Contains(type.FullName),
                            "Reading " + type.FullName + "." + member.Name + " ran its type initializer and threw " +
                            e.GetType().Name + ", so its contents were never inspected. Add it to " +
                            "UninspectableStatics with a Why, or make it readable — a table this gate cannot read " +
                            "is a table it does not gate.");
                        continue;
                    }

                    var hits = new SortedSet<string>(StringKeys(value).Where(k => k != null && names.Contains(k)),
                        StringComparer.Ordinal);
                    if (hits.Count != 0)
                        observed.Add(type.FullName + "." + member.Name + " -> " + string.Join(", ", hits));
                }
            }

            var declared = new SortedSet<string>(
                AllowedNameKeyedTables.Select(a => a.Type + "." + a.Member + " -> " + a.Names), StringComparer.Ordinal);

            var actual = WriteActual("name-keyed-tables", observed);
            Assert.True(declared.SetEquals(observed),
                Describe("The static string tables in Heddle.Generator.dll holding built-in extension names",
                    declared, observed, actual));
            AssertWhysNameTheirFate(AllowedNameKeyedTables.Select(a => (a.Type + "." + a.Member, a.Why)));
            Assert.All(UninspectableStatics, u => Assert.NotEmpty(u.Why));
        }

        /// <summary>The step-back list is gone from the binder, name and all — asserted structurally, because a
        /// deleted member can come back under another name and a source-text pin cannot tell them apart.</summary>
        [Fact]
        public void TheBinderCarriesNoPinnedExtensionList()
        {
            var info = typeof(gen::Heddle.Generator.Emit.ExtensionBinder.Info);
            Assert.Null(info.GetProperty("HasPinnedStepBackHook",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

            var binder = typeof(gen::Heddle.Generator.Emit.ExtensionBinder);
            foreach (var field in binder.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                Assert.False(typeof(IEnumerable<string>).IsAssignableFrom(field.FieldType),
                    "ExtensionBinder carries a static string collection again (" + field.Name +
                    "). A set of extension names is exactly what the shared table replaced.");
        }

        /// <summary>Every extension name the engine registers, read off the assembly rather than listed — a name
        /// added to the engine is covered by this gate the day it is added.</summary>
        private static IReadOnlyList<string> BuiltInNames()
        {
            var attr = typeof(global::Heddle.Attributes.ExtensionNameAttribute);
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in typeof(global::Heddle.HeddleTemplate).Assembly.GetTypes())
                foreach (var data in type.GetCustomAttributesData())
                    if (data.AttributeType == attr && data.ConstructorArguments.Count != 0 &&
                        data.ConstructorArguments[0].Value is string name && name.Length != 0)
                        names.Add(name);
            return names.ToList();
        }

        /// <summary>The namespaces the engine declares extensions in, computed off the same attribute
        /// <see cref="BuiltInNames"/> reads.</summary>
        private static IReadOnlyList<string> ExtensionNamespaces()
        {
            var attr = typeof(global::Heddle.Attributes.ExtensionNameAttribute);
            var namespaces = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in typeof(global::Heddle.HeddleTemplate).Assembly.GetTypes())
                foreach (var data in type.GetCustomAttributesData())
                    if (data.AttributeType == attr && !string.IsNullOrEmpty(type.Namespace))
                        namespaces.Add(type.Namespace);
            return namespaces.ToList();
        }

        /// <summary>The syntactic position that makes a literal a name, or <c>null</c> when it is just text.
        /// Determined from ancestors alone — no symbols, no vocabulary.</summary>
        private static string NameishForm(LiteralExpressionSyntax literal)
        {
            SyntaxNode node = literal;
            while (node.Parent is ParenthesizedExpressionSyntax)
                node = node.Parent;
            var parent = node.Parent;

            if (parent is BinaryExpressionSyntax binary &&
                (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)))
                return "equality";
            if (parent is CaseSwitchLabelSyntax)
                return "case";
            if (parent is ConstantPatternSyntax)
                return "pattern";
            if (parent is InitializerExpressionSyntax)
                return "initializer";

            var argument = parent as ArgumentSyntax;
            if (argument != null)
            {
                if (argument.Parent is BracketedArgumentListSyntax bracketed)
                    return bracketed.Parent is ImplicitElementAccessSyntax ||
                           bracketed.Parent is ElementAccessExpressionSyntax
                        ? "element-access"
                        : null;
                var list = argument.Parent as ArgumentListSyntax;
                var invocation = list == null ? null : list.Parent as InvocationExpressionSyntax;
                var method = invocation == null ? null : InvokedName(invocation.Expression);
                return method != null && Array.IndexOf(NameishMethods, method) >= 0 ? "call:" + method : null;
            }

            var initializer = parent as EqualsValueClauseSyntax;
            if (initializer != null)
            {
                if (initializer.Parent is ParameterSyntax)
                    return "default-parameter";
                var declarator = initializer.Parent as VariableDeclaratorSyntax;
                var declaration = declarator == null ? null : declarator.Parent as VariableDeclarationSyntax;
                if (declaration != null && declaration.Parent is FieldDeclarationSyntax field &&
                    field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    return "const-field";
                if (declaration != null && declaration.Parent is LocalDeclarationStatementSyntax local &&
                    local.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    return "const-local";
            }

            return null;
        }

        /// <summary>Whether a node is a whole dotted name and nothing else — a qualified name, or a member access
        /// built only out of names. A member-access chain carrying arguments is excluded because its text contains
        /// its arguments' text, which would count the literals inside it a second time.</summary>
        private static bool IsMaximalDottedName(SyntaxNode node)
        {
            if (!(node is NameSyntax) && !(node is MemberAccessExpressionSyntax))
                return false;
            if (node.Parent is NameSyntax || node.Parent is MemberAccessExpressionSyntax)
                return false;
            foreach (var descendant in node.DescendantNodes())
                if (!(descendant is SimpleNameSyntax) && !(descendant is NameSyntax) &&
                    !(descendant is MemberAccessExpressionSyntax) && !(descendant is PredefinedTypeSyntax))
                    return false;
            return true;
        }

        private static string InvokedName(ExpressionSyntax expression)
        {
            var member = expression as MemberAccessExpressionSyntax;
            if (member != null)
                return member.Name.Identifier.ValueText;
            var binding = expression as MemberBindingExpressionSyntax;
            if (binding != null)
                return binding.Name.Identifier.ValueText;
            var name = expression as SimpleNameSyntax;
            return name == null ? null : name.Identifier.ValueText;
        }

        /// <summary>Whether a literal has the shape a name has: the grammar's own identifier
        /// (<c>IDENTIFIER_START IDENTIFIER_PART*</c>), which is what an extension name, a directive and a prop are
        /// spelled as. A message, a format string, a path fragment and a dotted type name are not names by this
        /// measure; the dotted ones are gated by
        /// <see cref="TheGeneratorSpellsNoEngineExtensionTypeItCouldBind"/>.</summary>
        private static bool IsIdentifierShaped(string value)
        {
            if (value.Length == 0 || value.Length > 64)
                return false;
            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;
            for (var i = 1; i < value.Length; i++)
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                    return false;
            return true;
        }

        private static bool IsStringKeyed(Type type)
        {
            if (type == typeof(string))
                return false;
            if (typeof(IEnumerable<string>).IsAssignableFrom(type))
                return true;
            foreach (var candidate in type.GetInterfaces().Concat(new[] { type }))
            {
                if (!candidate.IsGenericType)
                    continue;
                var definition = candidate.GetGenericTypeDefinition();
                if ((definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>) ||
                     definition == typeof(ISet<>)) && candidate.GetGenericArguments()[0] == typeof(string))
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> StringKeys(object value)
        {
            if (value == null)
                yield break;
            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (var key in dictionary.Keys)
                    yield return key as string;
                yield break;
            }

            var strings = value as IEnumerable<string>;
            if (strings != null)
            {
                foreach (var item in strings)
                    yield return item;
                yield break;
            }

            var items = value as IEnumerable;
            if (items != null)
                foreach (var item in items)
                    yield return item as string;
        }

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        private static IReadOnlyList<(string Path, SyntaxTree Tree)> ParseCompileInputs()
        {
            var root = RepoRoot();
            var parsed = new List<(string, SyntaxTree)>();
            foreach (var relative in ManifestPaths())
            {
                var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                    parsed.Add((relative, CSharpSyntaxTree.ParseText(File.ReadAllText(full))));
            }

            return parsed;
        }

        private static IReadOnlyList<string> ManifestPaths()
        {
            using (var stream = GeneratorAssembly.GetManifestResourceStream(ManifestResourceName))
            {
                Assert.True(stream != null,
                    "Heddle.Generator.dll carries no '" + ManifestResourceName + "' resource. It is written and " +
                    "embedded by the HeddleWriteCompileManifest target in " +
                    "src/Heddle.Generator/Heddle.Generator.Common.props; without it this gate does not know what " +
                    "the assembly is built from.");
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd().Replace("\r\n", "\n").Split('\n')
                        .Select(line => line.Trim()).Where(line => line.Length != 0).ToList();
            }
        }

        /// <summary>The file set the manifest replaces, kept as its lower bound.</summary>
        private static IReadOnlyList<string> GeneratorDirectorySources(string root)
        {
            var directory = Path.Combine(root, "src", "Heddle.Generator");
            var files = new List<string>();
            foreach (var path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (relative.Contains("/obj/") || relative.Contains("/bin/"))
                    continue;
                files.Add(relative);
            }

            return files;
        }

        private static string RepoRoot()
        {
            var directory = Path.GetDirectoryName(typeof(ExtensionAgnosticismTests).Assembly.Location);
            for (var i = 0; i < 10 && !string.IsNullOrEmpty(directory);
                 i++, directory = Path.GetDirectoryName(directory))
                if (File.Exists(Path.Combine(directory, "src", "Heddle.Generator", "HeddleTemplateGenerator.cs")))
                    return directory;

            Assert.Fail("The repository root was not found by walking up from " +
                        typeof(ExtensionAgnosticismTests).Assembly.Location +
                        ". This gate reads the generator's compile inputs and must not be skipped.");
            return null;
        }

        /// <summary>Writes what was observed beside the test assembly, so making a red gate green again starts from
        /// the current list rather than from transcribing a failure message — the shape
        /// <c>TestClassInventory</c> established.</summary>
        private static string WriteActual(string name, IEnumerable<string> observed)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "ExtensionAgnosticism." + name + ".actual.txt");
            File.WriteAllText(path, string.Join("\n", observed) + "\n");
            return path;
        }

        private static string Describe(string what, IEnumerable<string> declared, IEnumerable<string> observed,
            string actual)
        {
            var d = new SortedSet<string>(declared, StringComparer.Ordinal);
            var o = new SortedSet<string>(observed, StringComparer.Ordinal);
            var gone = d.Except(o).ToList();
            var added = o.Except(d).ToList();
            return what + " drifted from the ledger in src/Heddle.Generator.Tests/ExtensionAgnosticismTests.cs."
                   + "\n  Declared but NOT found (" + gone.Count + "): "
                   + (gone.Count == 0 ? "(none)" : string.Join("; ", gone))
                   + "\n  Found but NOT declared (" + added.Count + "): "
                   + (added.Count == 0 ? "(none)" : string.Join("; ", added))
                   + "\n  A row that vanished is hardcoded knowledge that was deleted: delete its row too."
                   + " A row that appeared is hardcoded knowledge that was added: write down what it is and when"
                   + " it goes, or bind to the extension instead of naming it."
                   + "\n  The current list is in '" + actual + "'.";
        }

        private static void AssertWhysNameTheirFate(IEnumerable<(string Row, string Why)> rows)
        {
            foreach (var (row, why) in rows)
                Assert.True(why != null && Regex.IsMatch(why, @"(permanent|retires in Stage [0-9])$"),
                    "The ledger row '" + row + "' does not say what becomes of it. A Why ends either in " +
                    "'permanent' — the construct has its own emission shape and always will — or in " +
                    "'retires in Stage N', naming the stage that deletes it.");
        }
    }
}
