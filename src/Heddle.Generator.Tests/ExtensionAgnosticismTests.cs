extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <b>The architectural gate.</b> The generator is a compiler for the Heddle language, not a catalogue of the
    /// extensions that ship with it. Every extension name it hardcodes is a third-party extension it silently
    /// cannot serve — that is what the private four-name step-back list was, and five of the nine extensions
    /// sharing one hook body were missing from it for no reason but its length.
    /// <para>So the rule is stated where it can regress: <c>src/Heddle.Generator/**</c> contains no
    /// <c>Heddle.Extensions.</c> type literal, no comparison against the engine's assembly name, and no
    /// <c>== "&lt;extension name&gt;"</c> — except for the entries below, each of which names a construct with its
    /// own emission shape rather than a bound extension the emitter could have dispatched generically. The
    /// allowlist is set-equality gated, counts included: it can only be added to by writing down what was added and
    /// why, which is the opposite of how the last name list grew.</para>
    /// <para>Pinned by source text, as <c>EmitterSharedRuleAdoptionTests</c> is, because the thing being prevented
    /// is a literal — reflection cannot see one.</para>
    /// </summary>
    public class ExtensionAgnosticismTests
    {
        /// <summary>One sanctioned literal comparison against a built-in extension name.</summary>
        private static readonly (string File, string Name, int Count, string Why)[] AllowedNameCompares =
        {
            // The four language DIRECTIVES, as the fallback for a name the binder resolves to nothing. The primary
            // rule is [ZeroOutput] off the binder; this list is what answers against an engine reference older than
            // that attribute, which v2.0.0 is. Delete it when the supported floor passes v2.0.0, not before.
            ("Emit/TemplateEmitter.cs", "model", 2, "zero-output fallback for a pre-[ZeroOutput] engine reference"),
            ("Emit/TemplateEmitter.cs", "using", 2, "zero-output fallback for a pre-[ZeroOutput] engine reference"),
            ("Emit/TemplateEmitter.cs", "import", 1, "zero-output fallback for a pre-[ZeroOutput] engine reference"),
            ("Emit/TemplateEmitter.cs", "profile", 2, "zero-output fallback, plus the running-profile flip"),

            // Two constructs the emitter emits itself rather than binding: @out writes a slot-mode projection
            // through PrecompiledRuntime.BindOut, and @partial resolves a template key at build time. Neither is a
            // PrecompiledRuntime.Bind of an extension instance, so neither has a generic arm to fall into.
            ("Emit/TemplateEmitter.cs", "out", 1, "@out has its own projection emission, not a Bind"),
            ("Emit/TemplateEmitter.cs", "partial", 1, "@partial resolves a template key, not a Bind")
        };

        /// <summary>One sanctioned <c>Heddle.Extensions.</c> type literal, with the number of times it is written.</summary>
        private static readonly (string File, string Type, int Count, string Why)[] AllowedTypeLiterals =
        {
            ("Emit/TemplateEmitter.cs", "Heddle.Extensions.OutExtension", 3,
                "the @out projection constructs the type it binds; PrecompiledRuntime.BindOut's parameter type is " +
                "what enforces the contract in the consumer's own compiler"),
            ("Emit/TemplateEmitter.cs", "Heddle.Extensions.EmptyExtension", 2,
                "the unnamed carrier, chosen by the running output profile rather than by any name in the template"),
            ("Emit/TemplateEmitter.cs", "Heddle.Extensions.EmptyHtmlExtension", 2,
                "the unnamed carrier's HTML twin, same reason")
        };

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

        private static IReadOnlyList<(string Path, string Text)> GeneratorSources()
        {
            var root = GeneratorRoot();
            var files = new List<(string, string)>();
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.DirectorySeparatorChar, '/');
                // obj/ holds generated assembly-info and the linked shared sources' build output, neither of which
                // is what this gate is about.
                if (relative.StartsWith("obj/", StringComparison.Ordinal) ||
                    relative.StartsWith("bin/", StringComparison.Ordinal))
                    continue;
                files.Add((relative, File.ReadAllText(path)));
            }

            Assert.True(files.Count > 10, "The generator's sources were not found under " + root + ".");
            return files;
        }

        private static string GeneratorRoot()
        {
            var dir = Path.GetDirectoryName(typeof(ExtensionAgnosticismTests).Assembly.Location);
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(dir, "Heddle.Generator");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "HeddleTemplateGenerator.cs")))
                    return candidate;
            }

            Assert.Fail("src/Heddle.Generator was not found by walking up from " +
                        typeof(ExtensionAgnosticismTests).Assembly.Location +
                        ". This gate reads the generator's sources and must not be skipped.");
            return null;
        }

        /// <summary>No <c>name == "…"</c> against an extension the engine registers, outside the allowlist. The
        /// three arms this gate was written for — <c>@list</c>, <c>@for</c> and the engine branch trio — are gone:
        /// one bound-extension arm serves every extension, built in or not.</summary>
        [Fact]
        public void TheGeneratorComparesNoNameAgainstABuiltInExtension()
        {
            var observed = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (path, text) in GeneratorSources())
            foreach (var name in BuiltInNames())
            {
                var count = Regex.Matches(text, @"[!=]=\s*""" + Regex.Escape(name) + @"""").Count;
                if (count != 0)
                    observed.Add(path + " " + name + " x" + count);
            }

            var allowed = new SortedSet<string>(
                AllowedNameCompares.Select(a => a.File + " " + a.Name + " x" + a.Count), StringComparer.Ordinal);
            Assert.Equal(allowed.ToArray(), observed.ToArray());
        }

        /// <summary>No <c>Heddle.Extensions.</c> type literal outside the allowlist — the emitter names the type it
        /// binds through the binder's <c>GlobalName</c>/<c>BareTypeName</c>, so a subclass, a nested type or a
        /// replacement registered by a package is spelled correctly without the emitter knowing it exists.</summary>
        [Fact]
        public void TheGeneratorSpellsNoEngineExtensionTypeItCouldBind()
        {
            var counted = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (path, text) in GeneratorSources())
            foreach (var group in Regex.Matches(text, @"Heddle\.Extensions\.(?<type>\w+)")
                         .Cast<Match>().GroupBy(m => m.Groups["type"].Value))
                counted.Add(path + " Heddle.Extensions." + group.Key + " x" + group.Count());

            var allowed = new SortedSet<string>(
                AllowedTypeLiterals.Select(a => a.File + " " + a.Type + " x" + a.Count), StringComparer.Ordinal);
            Assert.Equal(allowed.ToArray(), counted.ToArray());
            Assert.All(AllowedTypeLiterals, a => Assert.NotEmpty(a.Why));
            Assert.All(AllowedNameCompares, a => Assert.NotEmpty(a.Why));
        }

        /// <summary>No comparison against the engine's assembly NAME. "Is this the engine's own extension?" is a
        /// symbol question with a symbol answer — <c>ExtensionBinder</c> asks it with
        /// <c>SymbolEqualityComparer</c> against <c>AbstractExtension.ContainingAssembly</c> — and a string compare
        /// would answer it wrongly for an ILMerged, aliased or renamed engine.</summary>
        [Fact]
        public void TheGeneratorComparesNothingAgainstTheEngineAssemblyName()
        {
            foreach (var (path, text) in GeneratorSources())
            {
                Assert.DoesNotMatch(@"[!=]=\s*""Heddle""", text);
                Assert.DoesNotMatch(@"""Heddle""\s*[!=]=", text);
                Assert.False(text.Contains("AssemblyName == \"") || text.Contains("Name == \"Heddle\""),
                    path + " compares an assembly name as a string.");
            }
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
                Assert.False(typeof(System.Collections.Generic.IEnumerable<string>).IsAssignableFrom(field.FieldType),
                    "ExtensionBinder carries a static string collection again (" + field.Name +
                    "). A set of extension names is exactly what the probe and the shared table replaced.");
        }
    }
}
