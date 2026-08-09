using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A template that declares no <c>@model</c> has two model types, not one: the build's, which is whatever the
    /// generator was told (nothing, so <c>object</c>, or a <c>ModelType</c> item metadatum), and the host's, which is
    /// the <c>CompileContext</c> the request arrives with. The engine uses the host's; the generated code was typed
    /// against the build's. Until the gauntlet compared them the registry served the build's answer to a host that
    /// had asked for something else, and nothing anywhere noticed.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class AmbientModelRoutingTests : PrecompiledRegistryTestBase
    {
        private const string ModelTypeName = "Heddle.Generator.IntegrationTests.Fixtures.InternalMemberModel";

        /// <summary>An <c>internal</c> member: readable by the engine's typed member tier, invisible to the binder
        /// the untyped tier uses (which binds in <c>Heddle</c>'s assembly context). The read that answers
        /// differently depending on which model type compiled it.</summary>
        private const string InternalRead = "[@(Secret)]\n";

        /// <summary>A public member, which both tiers read the same way whatever typed them — the control that keeps
        /// the untyped leg about routing rather than about visibility.</summary>
        private const string PublicRead = "[@(Title)]\n";

        private static InternalMemberModel Model() => new InternalMemberModel();

        private static Dictionary<string, Dictionary<string, string>> TypedBy(string key) =>
            new Dictionary<string, Dictionary<string, string>>
            {
                [key] = new Dictionary<string, string>
                {
                    ["build_metadata.AdditionalFiles.ModelType"] = ModelTypeName
                }
            };

        /// <summary>Renders <paramref name="key"/> through the registration → resolver → gauntlet → adapter seam
        /// under a context typed <paramref name="requested"/>, with the template staged on disk so a decline has a
        /// dynamic tier to fall to. Returns the bytes and every fallback the request raised.</summary>
        private static (string output, IReadOnlyList<PrecompiledFallbackEvent> fallbacks) ThroughResolver(
            DifferentialHarness.GenResult gen, string key, string content, ExType requested, object model)
        {
            PrecompiledTemplates.Register(gen.Assembly);
            var dir = DifferentialHarness.StageCorpus(new[] { (key, content) });
            try
            {
                var options = new TemplateOptions
                {
                    RootPath = dir + Path.DirectorySeparatorChar,
                    FileNamePostfix = ".heddle"
                };
                using (var guard = FallbackGuard.Install())
                {
                    var resolver = new TemplateResolver(Path.Combine(dir, "root.marker"));
                    var template = resolver.GetTemplate(key, string.Empty, out _,
                        new CompileContext(options, requested), TemplatePathType.None);
                    Assert.NotNull(template);
                    Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                    return (template.Generate(model), guard.Events);
                }
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(dir);
            }
        }

        private static string DynamicReference(string content, ExType requested, object model)
        {
            var template = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), requested));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(model);
        }

        /// <summary>
        /// The divergence, and the fix in one pass. The build precompiled an untyped template, so its
        /// <c>Secret</c> read went through the untyped binder and cannot see an <c>internal</c> member of the
        /// consumer's own assembly; the host then asked for the template with the model typed, which is the one
        /// configuration in which the engine reads that member. The precompiled entry's own answer is still there to
        /// be inspected and is still the wrong one — that is the defect — but the request no longer reaches it: the
        /// gauntlet declines on the model type and the dynamic tier answers.
        /// </summary>
        [Fact]
        public void AnUntypedEntryIsDeclinedForATypedRequestAndTheDynamicTierAnswers()
        {
            const string key = "views/ambient-internal.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, InternalRead) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var typed = new ExType(typeof(InternalMemberModel));
            var expected = DynamicReference(InternalRead, typed, Model());
            Assert.Equal("[s3cret]\n", expected);

            // What the entry the registry holds does with the same model: typed against object, its read goes to a
            // binder that cannot see the member at all, so it does not merely write different bytes — it faults.
            var direct = Assert.Throws<Microsoft.CSharp.RuntimeBinder.RuntimeBinderException>(
                () => DifferentialHarness.RenderGenerated(gen, key, Model()));
            Assert.Contains("inaccessible due to its protection level", direct.Message);

            var (served, fallbacks) = ThroughResolver(gen, key, InternalRead, typed, Model());

            var fallback = Assert.Single(fallbacks);
            Assert.Equal(PrecompiledFallbackReason.ModelTypeMismatch, fallback.Reason);
            Assert.Equal(key, fallback.TemplateKey);
            // Both halves of the detail, either side of the assembly name the core library answers to on this
            // target framework — which is a property of the runtime, not of the check.
            Assert.StartsWith("Model: manifest=System.Object, ", fallback.Detail);
            Assert.EndsWith(" request=" + ModelTypeName + ", Heddle.Generator.IntegrationTests", fallback.Detail);
            Assert.Equal("HED7101", fallback.DiagnosticId);
            Assert.Equal(expected, served);
        }

        /// <summary>
        /// The other half of the same rule: an untyped request against an untyped entry agrees, so the entry is
        /// served — the check declines a mismatch and nothing else. Without this leg the fix would be
        /// indistinguishable from switching the tier off for every model-less template.
        /// </summary>
        [Fact]
        public void AnUntypedEntryStaysOnThePrecompiledTierForAnUntypedRequest()
        {
            const string key = "views/ambient-untyped.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, PublicRead) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (served, fallbacks) = ThroughResolver(gen, key, PublicRead, ExType.Dynamic, Model());

            Assert.Empty(fallbacks);
            Assert.Equal(DynamicReference(PublicRead, ExType.Dynamic, Model()), served);
            Assert.Equal("[public]\n", served);
        }

        /// <summary>
        /// The metadata-typed shape, where the build's model type is a real type rather than <c>object</c>. A host
        /// asking for the very type the build was given is the configuration the metadatum documents, and the
        /// gauntlet lets it through: the precompiled tier serves, byte-for-byte what the engine would have written.
        /// </summary>
        [Fact]
        public void AMetadataTypedEntryIsServedToTheRequestThatNamesTheSameType()
        {
            const string key = "views/ambient-metadata-match.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, InternalRead) },
                perFileMetadata: TypedBy(key));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var typed = new ExType(typeof(InternalMemberModel));
            var (served, fallbacks) = ThroughResolver(gen, key, InternalRead, typed, Model());

            Assert.Empty(fallbacks);
            Assert.Equal("[s3cret]\n", served);
            Assert.Equal(DynamicReference(InternalRead, typed, Model()), served);
        }

        /// <summary>
        /// And its mirror: the same entry, typed by metadata the engine never sees, asked for by a host that left
        /// its context untyped. The build's type and the request's disagree, so the entry is declined and the
        /// dynamic tier — which types the template exactly as the host asked — writes the bytes.
        /// </summary>
        [Fact]
        public void AMetadataTypedEntryIsDeclinedForAnUntypedRequest()
        {
            const string key = "views/ambient-metadata-mismatch.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, PublicRead) },
                perFileMetadata: TypedBy(key));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (served, fallbacks) = ThroughResolver(gen, key, PublicRead, ExType.Dynamic, Model());

            var fallback = Assert.Single(fallbacks);
            Assert.Equal(PrecompiledFallbackReason.ModelTypeMismatch, fallback.Reason);
            Assert.Equal(DynamicReference(PublicRead, ExType.Dynamic, Model()), served);
            Assert.Equal("[public]\n", served);
        }

        /// <summary>
        /// A template that pins its own model is not ambient, and the host's context type does not participate in
        /// its typing on either tier — <c>ModelExtension.InitStart</c> overwrites whatever the host supplied. So the
        /// entry is served to a request whose context says something else entirely, which is the case identity would
        /// have wrongly refused had the manifest not recorded where its model type came from.
        /// </summary>
        [Fact]
        public void ADeclaredModelEntryIsServedWhateverTheRequestsContextSays()
        {
            const string key = "views/declared-model.heddle";
            var content = "@model(){{" + ModelTypeName + "}}@\\\n" + InternalRead;
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (served, fallbacks) = ThroughResolver(gen, key, content, new ExType(typeof(Cart)), Model());

            Assert.Empty(fallbacks);
            Assert.Equal("[s3cret]\n", served);
        }
    }
}
