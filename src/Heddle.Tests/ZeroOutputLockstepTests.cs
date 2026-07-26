using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>Generator plan phase 2 WI7 (D8) — the zero-output lockstep gate, modeled on
    /// <see cref="DefaultFunctionLockstepTests"/> ("the table forgot a built-in is a red build").</para>
    /// <para>The runtime decides zero-output by <em>computation</em> — <c>returnTypeChainedPrevious == null</c>
    /// after the chain compiles, i.e. the extension's <c>InitStart</c> returned a null <c>ExType</c> — and the
    /// removal is observable as the chain's block being excised from the compiled working document. The generator
    /// decides it from a hardcoded four-name list (<c>TemplateEmitter.IsDirectiveName</c>), mirrored below. The
    /// drift this closes is structurally one-way: a new zero-output built-in changes runtime output with zero
    /// generator signal. When phase 1's <c>[ZeroOutput]</c> attribute ships, the mirror side of this guard becomes
    /// the attribute-derived set and the hardcoded list dies there, not here.</para>
    /// </summary>
    public class ZeroOutputLockstepTests
    {
        /// <summary>The generator's <c>TemplateEmitter.IsDirectiveName</c> list, mirrored (the generator assembly
        /// is not referenced from this project; <c>Heddle.Generator.Tests</c> pins the mirror against the real
        /// method).</summary>
        internal static readonly string[] GeneratorDirectiveNames = { "model", "using", "import", "profile" };

        private static IEnumerable<string> BuiltInExtensionNames()
            => typeof(HeddleTemplate).GetTypeInfo().Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IExtension).IsAssignableFrom(t))
                .SelectMany(t => t.GetCustomAttributes<ExtensionNameAttribute>(false).Select(a => a.Name))
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal);

        /// <summary>Compiles <c>[@name()]</c> and reads the compiled working document: a zero-output chain is
        /// removed from it (<c>RemoveEmptyItem</c>), a rendering one is not. Returns null when the probe did not
        /// compile cleanly — an extension that needs a body or a real parameter is not evidence either way, and
        /// the coverage floor below keeps that escape hatch from hollowing the guard out.</summary>
        private static bool? IsZeroOutputAtRuntime(string name)
        {
            HeddleTemplate.Configure(typeof(ZeroOutputLockstepTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                RootPath = Path.GetFullPath("TestTemplate"), TrimDirectiveLines = false
            };
            // Two shots: the bodiless call, then the bodied one — '@profile()' only compiles with its
            // '{{html|text}}' body, and a body is harmless for every other built-in.
            var template = new HeddleTemplate("[@" + name + "()]", new CompileContext(options, ExType.Dynamic));
            if (!template.CompileResult.Success)
                template = new HeddleTemplate("[@" + name + "(){{text}}]", new CompileContext(options, ExType.Dynamic));
            if (!template.CompileResult.Success)
                return null;

            var field = typeof(HeddleTemplate).GetField("_runtimeDocument",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var document = (RuntimeDocument) field.GetValue(template);
            if (document == null)
                return null;
            return document.Document == "[]";
        }

        [Fact]
        public void NoBuiltInIsZeroOutputOutsideTheGeneratorList()
        {
            var offenders = new List<string>();
            int covered = 0;

            foreach (var name in BuiltInExtensionNames())
            {
                var zeroOutput = IsZeroOutputAtRuntime(name);
                if (zeroOutput == null)
                    continue;
                covered++;
                if (zeroOutput.Value != GeneratorDirectiveNames.Contains(name, StringComparer.Ordinal))
                    offenders.Add(name + " (runtime zero-output: " + zeroOutput.Value + ")");
            }

            Assert.True(covered >= 10, $"Only {covered} built-ins were probed — the guard has gone hollow.");
            Assert.True(offenders.Count == 0,
                "Runtime zero-output classification disagrees with the generator's directive list for: "
                + string.Join(", ", offenders)
                + ". Add or remove the name on BOTH sides (TemplateEmitter.IsDirectiveName and this mirror).");
        }

        [Fact] // the other direction: every listed name really is zero-output at runtime
        public void EveryGeneratorDirectiveNameIsZeroOutputAtRuntime()
        {
            var unprobeable = new List<string>();
            foreach (var name in GeneratorDirectiveNames)
            {
                var zeroOutput = IsZeroOutputAtRuntime(name);
                if (zeroOutput == null)
                {
                    unprobeable.Add(name);
                    continue;
                }

                Assert.True(zeroOutput.Value,
                    $"'{name}' is on the generator's directive list but the runtime renders it.");
            }

            // '@import' is a removal tombstone (HED4003 on every call shape, import-removal-spec D4/D5): it stays
            // on the generator's list but can never compile, so it is unprobeable BY DESIGN. Any other name
            // joining it means a directive silently stopped compiling — red.
            Assert.Equal(new[] { "import" }, unprobeable.ToArray());
        }
    }
}
