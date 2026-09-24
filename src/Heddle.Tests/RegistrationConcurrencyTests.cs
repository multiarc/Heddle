using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Registration is documented as repeatable and legal after rendering has begun, which makes it a concurrent writer
    /// against every compile in flight. These pin that it cannot corrupt what a compile sees.
    /// <para>Both scenarios are regressions, reproduced before the fix: the type maps were assigned empty and then
    /// filled while unlocked readers looked them up, and the extension registry was mutated in place while unlocked
    /// readers enumerated it. The symptom was not a crash but a <b>phantom diagnostic</b> — a valid template failing to
    /// resolve a type it had just resolved, or an <c>@else</c> reporting no matching opener — which is worse, because a
    /// host sees a template error that is not there.</para>
    /// </summary>
    public class RegistrationConcurrencyTests
    {
        private const int Compiles = 600;

        /// <summary>Type resolution must not observe a half-built name map while a host registers.</summary>
        [Fact]
        public void CompilingWhileRegisteringNeverFailsToResolveAKnownType()
        {
            var failures = RunWhileRegistering("@model(){{System.DateTime}}\n@(Year)",
                new DateTime(2026, 7, 27));

            Assert.True(failures.Count == 0,
                "A compile racing Register produced " + failures.Count + " phantom failure(s): " +
                string.Join(" | ", failures));
        }

        /// <summary>The branch-set scan reads the extension registry; a concurrent registration must not empty it.</summary>
        [Fact]
        public void CompilingABranchSetWhileRegisteringNeverReportsAMissingOpener()
        {
            var failures = RunWhileRegistering("@model(){{dynamic}}@if(1==1){{yes}}@else(){{no}}", new object());

            Assert.True(failures.Count == 0,
                "A branch-set compile racing Register produced " + failures.Count + " phantom failure(s): " +
                string.Join(" | ", failures));
        }

        /// <summary>The LSP reads registered names for completion while a host may still be registering.</summary>
        [Fact]
        public void ReadingRegisteredNamesWhileRegisteringNeverThrows()
        {
            var assembly = typeof(RegistrationConcurrencyTests).Assembly;
            var stop = false;
            Exception caught = null;

            var writer = Task.Run(() =>
            {
                var n = 0;
                while (!Volatile.Read(ref stop))
                {
                    HeddleTemplate.Register(assembly);
                    TemplateFactory.AddExtensions(new[]
                    {
                        new ExtensionType("namer" + Interlocked.Increment(ref n), typeof(RacerExtension), false)
                    });
                }
            });

            try
            {
                for (var i = 0; i < Compiles; i++)
                    Assert.NotEmpty(ExtensionNames());
            }
            catch (Exception e)
            {
                caught = e;
            }
            finally
            {
                Volatile.Write(ref stop, true);
                // Joined synchronously on purpose: the writer races this thread over process-global registration
                // state, and the assertions below are only meaningful once it has actually stopped. An async test
                // would hand the wait back to the runner and let the next test start against a mutating registry.
#pragma warning disable xUnit1031
                writer.Wait();
#pragma warning restore xUnit1031
            }

            Assert.Null(caught);
        }

        /// <summary>
        /// Compiles the template repeatedly while a second thread registers, collecting every failure.
        /// <para>The writer adds a <b>fresh</b> extension name each iteration as well as re-registering the assembly.
        /// Both matter: <see cref="HeddleTemplate.Register(Assembly)"/> is idempotent per assembly, so on its own it
        /// stops writing the registry after the first call and would exercise only the type-map rebuild — a writer that
        /// does not write cannot catch a writer race.</para>
        /// </summary>
        private static List<string> RunWhileRegistering(string template, object model)
        {
            var assembly = typeof(RegistrationConcurrencyTests).Assembly;
            var failures = new List<string>();
            var stop = false;

            var writer = Task.Run(() =>
            {
                var n = 0;
                while (!Volatile.Read(ref stop))
                {
                    HeddleTemplate.Register(assembly);
                    TemplateFactory.AddExtensions(new[]
                    {
                        new ExtensionType("racer" + Interlocked.Increment(ref n), typeof(RacerExtension), false)
                    });
                }
            });

            try
            {
                for (var i = 0; i < Compiles; i++)
                {
                    try
                    {
                        using var compiled = new HeddleTemplate(template, new CompileContext(new TemplateOptions()));
                        if (!compiled.CompileResult.Success)
                            failures.Add("compile " + i + ": " + compiled.CompileResult);
                        else
                            compiled.Generate(model);
                    }
                    catch (Exception e)
                    {
                        failures.Add("compile " + i + " threw: " + e.GetType().Name + ": " + e.Message);
                    }

                    if (failures.Count > 3)
                        break;
                }
            }
            finally
            {
                Volatile.Write(ref stop, true);
                // Joined synchronously on purpose: the writer races this thread over process-global registration
                // state, and the assertions below are only meaningful once it has actually stopped. An async test
                // would hand the wait back to the runner and let the next test start against a mutating registry.
#pragma warning disable xUnit1031
                writer.Wait();
#pragma warning restore xUnit1031
            }

            return failures;
        }

        /// <summary>Registered under a fresh name per writer iteration; never called, only registered.</summary>
        [Heddle.Attributes.ExtensionName("racer-never-called")]
        public sealed class RacerExtension : Heddle.Core.AbstractExtension
        {
            public override object ProcessData(in Scope scope) => string.Empty;

            public override void RenderData(in Scope scope)
            {
            }
        }

        private static IReadOnlyCollection<string> ExtensionNames()
        {
            var method = typeof(TemplateFactory).GetMethod("RegisteredNames",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (IReadOnlyCollection<string>)method.Invoke(null, null);
        }
    }
}
