using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Heddle.Core;
using Heddle.Data;
using Heddle.Extensions;
using Heddle.Language;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>The precompilation tier used to <em>reimplement</em> what an extension's compile-time hook decides.
    /// <see cref="PrecompiledRuntime.Init"/> runs the hook instead, against a synthesized compile scope with the
    /// generated body supplied rather than compiled. This suite is the proof that the synthesis is faithful.</para>
    /// <para><b>Post-state equivalence.</b> For every built-in extension the engine registers, the same call is
    /// compiled twice through the real engine and once through <c>Init</c>; the first engine compile donates the
    /// body strategy the build tier will emit, the second is the comparison subject. Every declared field of the
    /// concrete extension type is compared, which is what catches <c>ListExtension._collectionCountReader</c> and
    /// <c>OutExtension._slotMode</c>/<c>_composedGuard</c> — state the build tier used to guess.</para>
    /// <para><b>What is and is not circular.</b> The body strategy and its locals flag are donated by an engine
    /// compile, so comparing them back proves only that the donation arrived. Everything else is a genuine
    /// comparison: <c>_innerResult</c> (supplied as the <em>declared</em> body text, not the donated one),
    /// <c>InnerExist</c>, <c>DirectRender</c>, <c>Position</c>, and every field the extension's own hook writes.
    /// The body typing each row declares is compared against what the hook actually handed the body compile, which
    /// no part of the synthesis can fake — the supply records it.</para>
    /// </summary>
    /// <summary>The fidelity corpus's model. Declared at namespace scope so <c>@model()</c> can name it the way a
    /// template would.</summary>
    public sealed class FidelityBag
    {
        public string Text { get; set; } = "t";
        public List<string> Items { get; set; } = new List<string> { "a" };
        public int Count { get; set; } = 2;
        public decimal Amount { get; set; } = 1m;
        public DateTime When { get; set; } = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        public Guid Id { get; set; } = Guid.Empty;
    }

    public class InitSynthesisFidelityTests
    {
        /// <summary>
        /// One built-in in one call shape. <see cref="BodyData"/>/<see cref="BodyChained"/> are the typing this
        /// extension's hook is expected to hand its body compile — the declaration half of the pin; the supply
        /// records the hook's real answer and <c>Init</c> refuses the site when the two disagree.
        /// <para>Bodies are deliberately free of definitions, raw-output blocks and branch sets, so the engine's
        /// document shaping is the identity on them and the declared body text is also the shaped text. A body that
        /// broke that rule would redden the <c>_innerResult</c> comparison rather than pass quietly.</para>
        /// </summary>
        private sealed class Row
        {
            internal Row(Type extension, string document, Type bodyData, Type bodyChained, string label = null)
            {
                Extension = extension;
                Document = document;
                BodyData = bodyData;
                BodyChained = bodyChained;
                Label = label ?? extension.Name;
            }

            /// <summary>The row's identity, so one extension can carry more than one call shape.</summary>
            internal string Label { get; }

            internal Type Extension { get; }
            internal string Document { get; }
            internal Type BodyData { get; }
            internal Type BodyChained { get; }

            /// <summary>The model type flowing into the call — what the engine's compiler resolved the call
            /// parameter to. Defaults to the document model.</summary>
            internal Type Parameter { get; set; }

            /// <summary>The enclosing scope's model type at the call. Defaults to the document model.</summary>
            internal Type Parent { get; set; }

            /// <summary>The chained type flowing into the call. A lone root-level call has none, and
            /// <c>InitializeTemplate</c> substitutes <see cref="object"/>.</summary>
            internal Type Chained { get; set; } = typeof(object);

            /// <summary>The active slot parameter type, for a call inside a slot-declaring definition body.</summary>
            internal Type Slot { get; set; }

            /// <summary>The extension whose call this row is about is not the only one the document compiles;
            /// this names it when the type alone is ambiguous.</summary>
            internal OutputProfile Profile { get; set; } = OutputProfile.Html;
        }

        private static readonly Row[] Corpus =
        {
            new Row(typeof(EmptyExtension), "@raw(Text){{@()}}", typeof(string), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(EmptyHtmlExtension), "@html(Text){{@()}}", typeof(string), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(AttrExtension), "@attr(Text){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(StringExtension), "@string(Text){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(JsExtension), "@js(Text){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(UrlExtension), "@url(Text){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(DateExtension), "@date(When){{yyyy}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(DateTime) },
            new Row(typeof(TimeExtension), "@time(When){{HH}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(DateTime) },
            new Row(typeof(IntegerExtension), "@int(Count){{N0}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(int) },
            new Row(typeof(MoneyExtension), "@money(Amount){{C}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(decimal) },
            new Row(typeof(GuidExtension), "@guid(Id){{D}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(Guid) },
            new Row(typeof(ListExtension), "@list(Items){{@()}}", typeof(string), typeof(int))
                { Parameter = typeof(List<string>) },
            new Row(typeof(ForIndexExtension), "@for(Count){{@()}}", typeof(FidelityBag), typeof(int))
                { Parameter = typeof(int) },
            new Row(typeof(IfExtension), "@if(Text){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(IfNotExtension), "@ifnot(Text){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(SwapExtension), "@swap(){{@()}}", typeof(object), typeof(FidelityBag)),
            new Row(typeof(ModelExtension), "@model(){{Heddle.Tests.FidelityBag}}",
                typeof(FidelityBag), typeof(object)),
            new Row(typeof(UsingExtension), "@using(){{System.Text}}", typeof(FidelityBag), typeof(object)),
            new Row(typeof(ProfileExtension), "@profile(){{text}}", typeof(FidelityBag), typeof(object)),
            new Row(typeof(PartialExtension), "@partial()", typeof(FidelityBag), typeof(object)),
            new Row(typeof(ParamExtension), "@param(Text)", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(OutExtension), "@out(){{@()}}", typeof(object), typeof(FidelityBag)),
            new Row(typeof(OutExtension),
                "@% <picker(out:: System.String)>{{@out(Text)}} :: Heddle.Tests.FidelityBag %@\n@picker(){{x}}",
                typeof(object), typeof(FidelityBag), "OutExtension.slot")
                { Parameter = typeof(string), Slot = typeof(string) },
            new Row(typeof(ElifExtension), "@if(Text){{a}}@elif(Text){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(string) },
            new Row(typeof(ElseExtension), "@if(Text){{a}}@else(){{@()}}", typeof(FidelityBag), typeof(object))
                { Parameter = typeof(FidelityBag) }
        };

        /// <summary>Registered types no call site can exercise, each with the reason.</summary>
        private static readonly Dictionary<string, string> NotExercisable = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["ImportExtension"] = "A tombstone. The name stays registered so '@import()' keeps its HED4003 " +
                                  "removal error, which the parser raises before any compile — so the extension " +
                                  "is never constructed and has no post-state to compare."
        };

        /// <summary>Every extension type the engine itself registers must have at least one corpus row. Read from
        /// the assembly rather than from <c>TemplateFactory</c>'s live registry, which is process-global and
        /// append-only: another test registering a foreign extension must not change what this suite covers.</summary>
        [Fact]
        public void EveryBuiltInExtensionHasACorpusRow()
        {
            var declared = new HashSet<Type>(Corpus.Select(r => r.Extension));
            var registered = BuiltInExtensionTypes();

            var missing = registered.Where(t => !declared.Contains(t) && !NotExercisable.ContainsKey(t.Name))
                .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var stale = declared.Where(t => !registered.Contains(t)).Select(t => t.Name)
                .OrderBy(n => n, StringComparer.Ordinal).ToList();

            Assert.True(missing.Count == 0,
                "Built-in extensions with no post-state fidelity row: " + string.Join(", ", missing) +
                ". An extension the synthesis has never been compared against is an extension the build tier is " +
                "still free to guess about.");
            Assert.True(stale.Count == 0,
                "Corpus rows naming types the engine no longer registers: " + string.Join(", ", stale) + ".");
            var staleExemptions = NotExercisable.Keys.Where(n => registered.All(t => t.Name != n))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(staleExemptions.Count == 0,
                "Exempted types the engine no longer registers: " + string.Join(", ", staleExemptions) + ".");
            Assert.All(NotExercisable.Values, why => Assert.False(string.IsNullOrWhiteSpace(why)));
            Assert.True(registered.Count >= 20,
                "Only " + registered.Count + " built-in extension types were discovered — the discovery walk " +
                "stopped working and this gate is measuring nothing.");
        }

        public static IEnumerable<object[]> CorpusRows() => Corpus.Select(r => new object[] { r.Label });

        /// <summary>
        /// The stage's core assertion. For one built-in: compile the call through the real engine twice, run
        /// <see cref="PrecompiledRuntime.Init"/> on a fresh instance with the equivalent site and the first
        /// compile's body, then compare the second compile's post-state against the synthesized one field by
        /// field — the base class's four body fields, <c>InnerExist</c>, <c>DirectRender</c>, <c>Position</c>, and
        /// every field the concrete type declares.
        /// </summary>
        [Theory]
        [MemberData(nameof(CorpusRows))]
        public void SynthesizedPostStateMatchesTheEnginesForEveryBuiltIn(string extensionName)
        {
            var row = Corpus.Single(r => r.Label == extensionName);

            var donor = CompileReference(row);
            var reference = CompileReference(row);

            var site = BuildSite(row, donor);
            var body = (IProcessStrategy) Field(typeof(AbstractExtension), "_processStrategy")
                .GetValue(donor.Extension);
            site.Body.NeedsLocals = (bool) Field(typeof(AbstractExtension), "_needsLocals").GetValue(donor.Extension);

            var synthesized = PrecompiledRuntime.Init(() => (AbstractExtension) Activator.CreateInstance(row.Extension),
                site, body);

            Assert.True(site.Fault == null,
                "Init faulted for " + extensionName + ": " + site.Fault?.Detail);
            Assert.IsType(row.Extension, synthesized);
            AssertPostStateEqual(reference.Extension, synthesized, extensionName);
        }

        /// <summary>
        /// The whole reason a call site can afford to run a real hook: the body typing the hook chooses is recorded
        /// and checked, so a build that assumed something else costs a tier instead of rendering wrong bytes. Every
        /// corpus row's declared body typing is that check, and this test proves the check is live by breaking it.
        /// </summary>
        [Fact]
        public void ADisagreementAboutBodyTypingCostsTheTemplate()
        {
            var row = Corpus.Single(r => r.Label == "ListExtension");
            var donor = CompileReference(row);
            var site = BuildSite(row, donor);
            site.Body.AssumedDataType = typeof(decimal);   // the hook hands its body the element type, not this

            var result = PrecompiledRuntime.Init(() => new ListExtension(), site,
                (IProcessStrategy) Field(typeof(AbstractExtension), "_processStrategy").GetValue(donor.Extension));

            Assert.NotNull(site.Fault);
            Assert.Equal(PrecompiledInitFaultScope.Template, site.Fault.Scope);
            Assert.Equal(PrecompiledFallbackReason.ExtensionInitTypingMismatch, site.Fault.Reason);
            Assert.NotSame(typeof(ListExtension), result.GetType());
        }

        /// <summary>A hook that throws costs its own call site and nothing else, and <c>Init</c> itself never
        /// throws — a throwing type initializer would poison every later use of the generated class.</summary>
        [Fact]
        public void AThrowingHookCostsOnlyTheCallSite()
        {
            var site = MinimalSite("boom");
            var result = PrecompiledRuntime.Init(() => new ThrowingHookExtension(), site, null);

            Assert.NotNull(site.Fault);
            Assert.Equal(PrecompiledInitFaultScope.CallSite, site.Fault.Scope);
            Assert.Null(site.Fault.Reason);
            Assert.IsNotType<ThrowingHookExtension>(result);
        }

        /// <summary>A factory that cannot construct its extension — the moved-assembly case — is the same call-site
        /// fault, which is why <c>Init</c> takes a factory rather than an instance.</summary>
        [Fact]
        public void AFactoryThatThrowsCostsOnlyTheCallSite()
        {
            var site = MinimalSite("gone");
            var result = PrecompiledRuntime.Init(() => throw new TypeLoadException("moved"), site, null);

            Assert.NotNull(site.Fault);
            Assert.Equal(PrecompiledInitFaultScope.CallSite, site.Fault.Scope);
            Assert.IsType<TypeLoadException>(site.Fault.Exception);
            Assert.NotNull(result);
        }

        /// <summary>A hook that compiles a second body would install this call's strategy on a document it does not
        /// belong to; the supply refuses the second consumption and the call site falls back.</summary>
        [Fact]
        public void AHookThatCompilesTwoBodiesCostsTheCallSite()
        {
            var site = MinimalSite("twice");
            site.Body.RawText = "x";
            site.Body.ShapedText = "x";

            var result = PrecompiledRuntime.Init(() => new TwoBodyExtension(), site, new NullStrategy());

            Assert.NotNull(site.Fault);
            Assert.Equal(PrecompiledInitFaultScope.CallSite, site.Fault.Scope);
            Assert.Contains("bodies", site.Fault.Detail, StringComparison.Ordinal);
            Assert.IsNotType<TwoBodyExtension>(result);
        }

        /// <summary>A hook that never compiles the body it was handed would drop the generated body silently.</summary>
        [Fact]
        public void AHookThatNeverCompilesTheSuppliedBodyCostsTheCallSite()
        {
            var site = MinimalSite("dropped");
            site.Body.RawText = "x";
            site.Body.ShapedText = "x";

            var result = PrecompiledRuntime.Init(() => new NoBodyExtension(), site, new NullStrategy());

            Assert.NotNull(site.Fault);
            Assert.Equal(PrecompiledInitFaultScope.CallSite, site.Fault.Scope);
            Assert.IsNotType<NoBodyExtension>(result);
        }

        /// <summary>A hook reporting compile errors is the dynamic tier's answer too, so the whole template moves
        /// rather than the one call.</summary>
        [Fact]
        public void AHookReportingCompileErrorsCostsTheTemplate()
        {
            var site = MinimalSite("bad");
            var result = PrecompiledRuntime.Init(() => new ErrorReportingExtension(), site, null);

            Assert.NotNull(site.Fault);
            Assert.Equal(PrecompiledInitFaultScope.Template, site.Fault.Scope);
            Assert.Equal(PrecompiledFallbackReason.ExtensionInitCompileError, site.Fault.Reason);
            Assert.Single(site.Fault.Errors);
        }

        /// <summary>Both new template-wide reasons are classified, so both can actually be reported. An
        /// unclassified reason is refused by <c>PrecompiledFallbackEvent</c>'s exhaustive switch.</summary>
        [Fact]
        public void TheNewFallbackReasonsAreReportable()
        {
            foreach (var reason in new[]
                     {
                         PrecompiledFallbackReason.ExtensionInitCompileError,
                         PrecompiledFallbackReason.ExtensionInitTypingMismatch
                     })
            {
                var evt = PrecompiledFallbackEvent.ForTemplate("some/key.heddle", reason, "detail", "HED7101");
                Assert.Equal(reason, evt.Reason);
                Assert.Throws<ArgumentException>(() =>
                    PrecompiledFallbackEvent.ForAssembly("Some.Assembly", reason, "detail", "HED7102"));
            }
        }

        /// <summary>Only a null argument throws. Everything else is recorded, because all of this runs from a
        /// generated static field initializer at registration.</summary>
        [Fact]
        public void OnlyANullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PrecompiledRuntime.Init(null, MinimalSite("x"), null));
            Assert.Throws<ArgumentNullException>(() =>
                PrecompiledRuntime.Init(() => new EmptyExtension(), null, null));
            Assert.Throws<ArgumentNullException>(() =>
                PrecompiledRuntime.InitDefinition(null, null, null, null, null));
        }

        /// <summary>The definition form runs two hooks — outer over caller content, inner over the definition
        /// body — and both take their recursion limit from the hook's own read of the options rather than from a
        /// value baked at build time.</summary>
        [Fact]
        public void InitDefinitionRunsBothHooksAndTakesTheRecursionLimitFromTheHook()
        {
            var site = MinimalSite("box");
            site.MaxRecursionCount = 17;
            site.DefinitionSlotType = typeof(string);
            site.Body.RawText = "caller";
            site.Body.ShapedText = "caller";
            site.DefinitionBody = new PrecompiledInitBody
            {
                RawText = "body",
                ShapedText = "body",
                AssumedDataType = null,
                AssumedChainedType = typeof(object)
            };
            site.Body.AssumedDataType = typeof(string);   // the slot type types the caller content
            site.Body.AssumedChainedType = typeof(object);

            var outer = PrecompiledRuntime.InitDefinition(site, new NullStrategy(), new NullStrategy(), null, null);

            Assert.Null(site.Fault);
            var carrierType = typeof(AbstractExtension).Assembly.GetType("Heddle.Core.DefinitionBaseExtension");
            Assert.IsType(carrierType, outer);
            Assert.Equal(17, Field(carrierType, "_maxRecursionCount").GetValue(outer));
            var inner = carrierType.GetProperty("DefinitionParameterTemplate",
                BindingFlags.Public | BindingFlags.Instance).GetValue(outer);
            Assert.NotNull(inner);
            Assert.Equal(17, Field(carrierType, "_maxRecursionCount").GetValue(inner));
            Assert.Equal(true, carrierType.GetProperty("SlotMode",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(outer));
        }

        /// <summary>
        /// The definition form against the engine's own two carriers. A definition invocation is the one call the
        /// engine initializes twice — an outer carrier over the caller content and an inner one over the definition
        /// body — and <see cref="PrecompiledRuntime.InitDefinition"/> has to reproduce both, including the recursion
        /// limit each reads from the options rather than from a constant baked at build time.
        /// </summary>
        [Fact]
        public void SynthesizedDefinitionCarriersMatchTheEngines()
        {
            const string document = "@% <box>{{@out()}} %@\n@box(){{C}}";
            var donor = CompileDefinition(document);
            var reference = CompileDefinition(document);

            var carrierType = typeof(AbstractExtension).Assembly.GetType("Heddle.Core.DefinitionBaseExtension");
            var innerProperty = carrierType.GetProperty("DefinitionParameterTemplate",
                BindingFlags.Public | BindingFlags.Instance);
            var strategy = Field(typeof(AbstractExtension), "_processStrategy");
            var locals = Field(typeof(AbstractExtension), "_needsLocals");

            var declaration = donor.Item.Context.GetDefenition("box").Position;
            var donorInner = (AbstractExtension) innerProperty.GetValue(donor.Extension);
            var referenceInner = (AbstractExtension) innerProperty.GetValue(reference.Extension);

            var site = new PrecompiledInitSite
            {
                ExtensionName = donor.Item.ExtensionName,
                PositionStart = donor.Item.Position.StartIndex,
                PositionLength = donor.Item.Position.Length,
                // Taken from the parsed declaration, not from the reference carrier, so the Position comparison
                // below is a real one: the engine positions both carriers at the definition, not at the call.
                DefinitionPositionStart = declaration.StartIndex,
                DefinitionPositionLength = declaration.Length,
                SourceText = document,
                Body = new PrecompiledInitBody
                {
                    RawText = donor.Item.ParameterTemplate,
                    ShapedText = donor.Item.ParameterTemplate,
                    NeedsLocals = (bool) locals.GetValue(donor.Extension),
                    AssumedDataType = typeof(FidelityBag),
                    AssumedChainedType = typeof(object)
                },
                DefinitionBody = new PrecompiledInitBody
                {
                    RawText = "@out()",
                    ShapedText = "@out()",
                    NeedsLocals = (bool) locals.GetValue(donorInner),
                    AssumedDataType = typeof(FidelityBag),
                    AssumedChainedType = typeof(object)
                },
                DataType = typeof(FidelityBag),
                ChainedType = typeof(object),
                ParentType = typeof(FidelityBag),
                ModelType = typeof(FidelityBag),
                RootModelType = typeof(FidelityBag),
                OutputProfile = OutputProfile.Html,
                ExpressionMode = ExpressionMode.Native,
                TrimDirectiveLines = true,
                MaxRecursionCount = new TemplateOptions().MaxRecursionCount,
                CallShape = PrecompiledCallShape.None
            };

            var synthesized = PrecompiledRuntime.InitDefinition(site,
                (IProcessStrategy) strategy.GetValue(donorInner),
                (IProcessStrategy) strategy.GetValue(donor.Extension), null, null);

            Assert.True(site.Fault == null, "InitDefinition faulted: " + site.Fault?.Detail);
            AssertPostStateEqual(reference.Extension, synthesized, "definition outer carrier");
            AssertPostStateEqual(referenceInner, (AbstractExtension) innerProperty.GetValue(synthesized),
                "definition inner carrier");
        }

        private static Reference CompileDefinition(string document)
        {
            var carrierType = typeof(AbstractExtension).Assembly.GetType("Heddle.Core.DefinitionBaseExtension");
            var context = new CompileContext(new TemplateOptions(), new ExType(typeof(FidelityBag)));
            var result = new HeddleTemplate().Compile(document, context);
            Assert.True(result.Success,
                "The definition fidelity document does not compile: " +
                string.Join("; ", result.Errors.Select(e => e.Error)));
            var found = FindExtension(context, carrierType);
            Assert.True(found != null, "The engine compiled the definition without producing a carrier.");
            return found;
        }

        /// <summary>The substitute a faulted call site returns compiles the call's own source text and renders it,
        /// and does so at first render rather than at type-init — a compile needs an ambient request, which only a
        /// render establishes.</summary>
        [Fact]
        public void TheSubstituteRendersTheCallBySourceTextAtFirstRender()
        {
            var site = MinimalSite("boom");
            site.SourceText = "hello";
            site.ModelType = typeof(FidelityBag);

            var substitute = PrecompiledRuntime.Init(() => new ThrowingHookExtension(), site, null);
            Assert.NotNull(site.Fault);

            var strategy = new ExtensionStrategy(substitute);
            Assert.Equal("hello", PrecompiledRuntime.GenerateString(strategy, new FidelityBag(), null, null));
        }

        /// <summary>
        /// <para>The synthesis reconstructs the engine's four compile-time context types from what one call site
        /// records. Every public member of those types is either reconstructed or declared unreconstructible with a
        /// reason — the same ledger discipline the corpus intent and agnosticism gates use, so a member added later
        /// is a red test rather than a silent hole.</para>
        /// </summary>
        [Fact]
        public void EveryPublicContextMemberIsReconstructedOrDeclaredUnreconstructible()
        {
            var declared = new HashSet<string>(
                Reconstructed.Keys.Concat(NotReconstructible.Keys), StringComparer.Ordinal);
            var actual = new List<string>();
            foreach (var type in new[]
                     {
                         typeof(InitContext), typeof(CompileScope), typeof(CompileContext), typeof(ParseContext)
                     })
            {
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                       BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (member is MethodInfo method && method.IsSpecialName)
                        continue;
                    if (member is ConstructorInfo)
                        continue;
                    if (member.Name == "Equals" || member.Name == "GetHashCode" || member.Name == "ToString" ||
                        member.Name == "Dispose")
                        continue;
                    actual.Add(type.Name + "." + member.Name);
                }
            }

            var undeclared = actual.Distinct(StringComparer.Ordinal).Where(m => !declared.Contains(m))
                .OrderBy(m => m, StringComparer.Ordinal).ToList();
            var stale = declared.Where(m => !actual.Contains(m, StringComparer.Ordinal))
                .OrderBy(m => m, StringComparer.Ordinal).ToList();

            Assert.True(undeclared.Count == 0,
                "Public compile-context members the Init synthesis has never decided about: " +
                string.Join(", ", undeclared) +
                ". Each must be reconstructed from the call site or declared unreconstructible with a reason.");
            Assert.True(stale.Count == 0,
                "Declared members that no longer exist: " + string.Join(", ", stale) + ".");
            Assert.All(NotReconstructible.Values, why => Assert.False(string.IsNullOrWhiteSpace(why)));
        }

        /// <summary>Members the synthesis rebuilds from what the call site records, and where the value comes from.</summary>
        private static readonly Dictionary<string, string> Reconstructed = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["InitContext.ParameterTemplate"] = "PrecompiledInitSite.Body.RawText.",
            ["InitContext.CompileScope"] = "Synthesized from the site's types, options and namespace set.",
            ["InitContext.ParseContext"] = "Synthesized; carries the in-definition flag.",
            ["InitContext.CallCarriesValue"] = "Reads the witness's call parameter, whose shape the site records.",
            ["InitContext.IsChainedConsumer"] = "Reads the witness's chained-consumer flag, set from the site.",
            ["CompileScope.CompileErrors"] = "The synthesized context's list; what the hook adds is read back.",
            ["CompileScope.CompileWarnings"] = "The synthesized context's list.",
            ["CompileScope.Namespaces"] = "PrecompiledInitSite.Namespaces, the @using set in scope at the call.",
            ["CompileScope.ScopeType"] = "PrecompiledInitSite.ModelType.",
            ["CompileScope.RootScopeType"] = "PrecompiledInitSite.RootModelType.",
            ["CompileScope.Options"] = "Rebuilt from profile, expression mode, trim and max recursion.",
            ["CompileScope.CompileContext"] = "Synthesized.",
            ["CompileScope.CSharpContext"] = "Fresh, seeded with the site's namespace set.",
            ["CompileContext.CompileErrors"] = "Fresh list; the hook's additions are the template-wide fault.",
            ["CompileContext.CompileWarnings"] = "Fresh list.",
            ["CompileContext.ScopeType"] = "PrecompiledInitSite.ModelType.",
            ["CompileContext.RootScopeType"] = "PrecompiledInitSite.RootModelType.",
            ["CompileContext.Options"] = "Rebuilt from the site.",
            ["CompileContext.OutputProfile"] = "PrecompiledInitSite.OutputProfile.",
            ["CompileContext.SlotParameterType"] = "PrecompiledInitSite.SlotType, installed on the synthesized " +
                                                   "scope so a slot projection's hook reaches the same state.",
            ["CompileContext.AddDelayedCompileTemplate"] = "The queue is real and drained as the engine drains it.",
            ["ParseContext.Offset"] = "PrecompiledInitSite.PositionStart.",
            ["ParseContext.Errors"] = "A live list; the engine drains parse errors before compiling, so nothing " +
                                      "the hook adds here would have been surfaced on the dynamic tier either.",
            ["ParseContext.Warnings"] = "A live list; see ParseContext.Errors."
        };

        /// <summary>
        /// Members no call site can carry, each with the reason. An accurate boundary is worth more than a
        /// synthesized value that lies about what it is.
        /// </summary>
        private static readonly Dictionary<string, string> NotReconstructible = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["ParseContext.Tokens"] = "The token stream IS the parse tree of the enclosing document. A call site " +
                                      "records a span, not a tree, so a hook walking it sees an empty sequence. " +
                                      "This is the one synthesized member that is present and empty rather than " +
                                      "absent, and it is the opt-out attribute's primary use case.",
            ["ParseContext.SubContexts"] = "Same reason as Tokens: the sub-context list is the parse tree's shape.",
            ["ParseContext.DefinitionsBlock"] = "The enclosing document's definitions. A call site is not the " +
                                                "document, and carrying every definition in scope would put the " +
                                                "whole parse in a static field initializer.",
            ["ParseContext.GetDefenition"] = "Reads DefinitionsBlock.",
            ["ParseContext.DefenitionExists"] = "Reads DefinitionsBlock.",
            ["ParseContext.OutputChains"] = "The enclosing document's parsed call chains.",
            ["ParseContext.DefaultChains"] = "The enclosing document's default-output chains.",
            ["ParseContext.RawOutputItems"] = "The enclosing document's raw-output blocks.",
            ["ParseContext.SkippedTokens"] = "Parser bookkeeping over the enclosing document's token stream.",
            ["ParseContext.IsolateContextWithTree"] = "Copies a parse tree there is none of.",
            ["CompileContext.Compiled"] = "The enclosing compile's completion flag; a per-call-site synthesis has " +
                                          "no enclosing compile to be the completion of.",
            ["CompileContext.ControllerName"] = "Host-set on the enclosing compile and read by nothing in the " +
                                                "engine; carrying it would be carrying a value with no reader."
        };

        /// <summary>
        /// <para>The synthesized <c>InitContext.SourceItem</c> is a witness, not a parsed call: its call parameter
        /// carries a placeholder in whichever arm the site's shape names, and nothing else. That is only sound
        /// while the engine's whole read closure over <c>SourceItem</c> is the three things the witness answers —
        /// <c>SlotRules.HasOutValue</c>'s collapse to a bool, <c>IsChainedConsumer</c>, and <c>Position</c>.</para>
        /// <para>So the closure is read out of the engine's own source rather than trusted. A new read of
        /// <c>SourceItem</c>, or a new member read off it, reddens this.</para>
        /// </summary>
        [Fact]
        public void TheSourceItemReadClosureIsWhatTheWitnessAnswers()
        {
            var root = Path.Combine(RepoRoot(), "Heddle");
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                            // The synthesizer WRITES the witness; the closure this gate pins is who READS it.
                            Path.GetFileName(f) != "PrecompiledRuntime.cs")
                .OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.True(files.Count > 100, "The engine source walk found only " + files.Count + " files.");

            var sites = new List<string>();
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf("SourceItem", StringComparison.Ordinal) < 0)
                        continue;
                    if (lines[i].TrimStart().StartsWith("///", StringComparison.Ordinal) ||
                        lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                        continue;
                    sites.Add(name + ":" + lines[i].Trim());
                }
            }

            var expected = new[]
            {
                "InitContext.cs:internal OutputItem SourceItem;",
                "InitContext.cs:public bool CallCarriesValue => SourceItem != null && " +
                "SlotRules.HasOutValue(SourceItem.CallParameter);",
                "InitContext.cs:public bool IsChainedConsumer => SourceItem != null && " +
                "SourceItem.IsChainedConsumer;",
                "HeddleCompiler.cs:SourceItem = sourceItem",
                "OutExtension.cs:var source = initContext.SourceItem;"
            };

            Assert.Equal(expected.OrderBy(s => s, StringComparer.Ordinal).ToList(),
                sites.OrderBy(s => s, StringComparer.Ordinal).ToList());

            // The single read binds one local; every member it then touches must be one the witness answers.
            var outExtension = File.ReadAllText(Path.Combine(root, "Extensions", "OutExtension.cs"));
            var members = new HashSet<string>(
                Regex.Matches(outExtension, @"\bsource(?:\?)?\.(\w+)").Cast<Match>().Select(m => m.Groups[1].Value),
                StringComparer.Ordinal);
            Assert.Equal(new[] { "CallParameter", "IsChainedConsumer", "Position" },
                members.OrderBy(m => m, StringComparer.Ordinal).ToArray());

            // And the only thing done with CallParameter is the five-way value test the call shape reproduces.
            Assert.Contains("HasOutValue(source.CallParameter)", outExtension, StringComparison.Ordinal);
            Assert.DoesNotContain("source.CallParameter.", outExtension, StringComparison.Ordinal);
        }

        private static string RepoRoot([CallerFilePath] string thisFile = null)
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), ".."));

        // ---- harness ----------------------------------------------------------------------------------------

        private sealed class Reference
        {
            internal AbstractExtension Extension;
            internal OutputItem Item;
        }

        /// <summary>One real engine compile of the row's document, with the row's extension instance pulled out of
        /// the compile context's own record of what it built.</summary>
        private static Reference CompileReference(Row row)
        {
            var options = new TemplateOptions { OutputProfile = row.Profile };
            var context = new CompileContext(options, new ExType(typeof(FidelityBag)));
            var template = new HeddleTemplate();
            var result = template.Compile(row.Document, context);
            Assert.True(result.Success,
                "The fidelity corpus document for " + row.Label + " does not compile: " +
                string.Join("; ", result.Errors.Select(e => e.Error)));

            var found = FindExtension(context, row.Extension);
            if (found == null)
                throw new InvalidOperationException(
                    "The engine compiled '" + row.Document + "' without producing a " + row.Label + ".");
            return found;
        }

        /// <summary>The compile context's own record of what it built, which is where the reference instance and
        /// its parsed call item both come from.</summary>
        private static Reference FindExtension(CompileContext context, Type extensionType)
        {
            var items = (System.Collections.IDictionary) typeof(CompileContext)
                .GetProperty("CompiledItems", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(context);
            foreach (System.Collections.DictionaryEntry entry in items)
            {
                var element = entry.Value;
                var compiledItem = element.GetType().GetField("CompiledItem").GetValue(element);
                if (compiledItem == null)
                    continue;
                var extension = compiledItem.GetType()
                    .GetProperty("Extension", BindingFlags.Public | BindingFlags.Instance).GetValue(compiledItem);
                if (extension != null && extension.GetType() == extensionType)
                    return new Reference { Extension = (AbstractExtension) extension, Item = (OutputItem) entry.Key };
            }

            return null;
        }

        /// <summary>The call site as the build tier would record it, read off the reference compile's own parsed
        /// call item so positions, body text and call shape are the engine's, not this test's.</summary>
        private static PrecompiledInitSite BuildSite(Row row, Reference reference)
        {
            var item = reference.Item;
            return new PrecompiledInitSite
            {
                ExtensionName = item.ExtensionName,
                PositionStart = item.Position.StartIndex,
                PositionLength = item.Position.Length,
                SourceText = row.Document,
                Body = new PrecompiledInitBody
                {
                    RawText = item.ParameterTemplate,
                    ShapedText = item.ParameterTemplate,
                    AssumedDataType = row.BodyData,
                    AssumedChainedType = row.BodyChained
                },
                DataType = row.Parameter ?? typeof(FidelityBag),
                ChainedType = row.Chained,
                ParentType = row.Parent ?? typeof(FidelityBag),
                ModelType = typeof(FidelityBag),
                RootModelType = typeof(FidelityBag),
                SlotType = row.Slot,
                OutputProfile = row.Profile,
                ExpressionMode = ExpressionMode.Native,
                TrimDirectiveLines = true,
                MaxRecursionCount = new TemplateOptions().MaxRecursionCount,
                CallShape = ShapeOf(item),
                HasPropArguments = item.CallParameter.PropArguments != null,
                RootReference = item.CallParameter.RootReference,
                IsChainedConsumer = ChainedConsumer(item),
                InsideDefinition = InDefinition(item)
            };
        }

        private static PrecompiledCallShape ShapeOf(OutputItem item)
        {
            var call = item.CallParameter;
            if (call.NativeExpression != null)
                return PrecompiledCallShape.NativeExpression;
            if (call.ChainParameter != null)
                return PrecompiledCallShape.Chain;
            if (!string.IsNullOrEmpty(call.CSharpExpression))
                return PrecompiledCallShape.CSharpExpression;
            if (call.PropArguments != null)
                return PrecompiledCallShape.PropArguments;
            return call.ModelParameter != null && call.ModelParameter.Length > 0 &&
                   !string.IsNullOrEmpty(call.ModelParameter[0])
                ? PrecompiledCallShape.ModelPath
                : PrecompiledCallShape.None;
        }

        private static bool ChainedConsumer(OutputItem item)
            => (bool) typeof(OutputItem)
                .GetProperty("IsChainedConsumer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(item);

        private static bool InDefinition(OutputItem item)
        {
            if (item.Context == null)
                return false;
            return (bool) typeof(ParseContext)
                .GetProperty("InDefintionContext", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(item.Context);
        }

        private static PrecompiledInitSite MinimalSite(string name) => new PrecompiledInitSite
        {
            ExtensionName = name,
            PositionStart = 0,
            PositionLength = 1,
            SourceText = string.Empty,
            Body = new PrecompiledInitBody { AssumedChainedType = typeof(object) },
            ChainedType = typeof(object),
            OutputProfile = OutputProfile.Html,
            ExpressionMode = ExpressionMode.Native,
            TrimDirectiveLines = true,
            MaxRecursionCount = new TemplateOptions().MaxRecursionCount
        };

        private static void AssertPostStateEqual(AbstractExtension reference, AbstractExtension synthesized,
            string what)
        {
            var abstractType = typeof(AbstractExtension);
            AssertEqual(what, "_processStrategy is present",
                Field(abstractType, "_processStrategy").GetValue(reference) != null,
                Field(abstractType, "_processStrategy").GetValue(synthesized) != null);
            AssertEqual(what, "_needsLocals",
                Field(abstractType, "_needsLocals").GetValue(reference),
                Field(abstractType, "_needsLocals").GetValue(synthesized));
            AssertEqual(what, "_innerResult",
                Field(abstractType, "_innerResult").GetValue(reference),
                Field(abstractType, "_innerResult").GetValue(synthesized));
            AssertEqual(what, "DirectRender",
                Field(abstractType, "DirectRender").GetValue(reference),
                Field(abstractType, "DirectRender").GetValue(synthesized));
            AssertEqual(what, "InnerExist", InnerExist(reference), InnerExist(synthesized));
            AssertEqual(what, "Position.StartIndex", reference.Position.StartIndex,
                synthesized.Position.StartIndex);
            AssertEqual(what, "Position.Length", reference.Position.Length, synthesized.Position.Length);

            for (var type = reference.GetType(); type != null && type != abstractType; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                     BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    AssertComparable(what, type.Name + "." + field.Name, field.GetValue(reference),
                        field.GetValue(synthesized));
                }
            }
        }

        private static bool InnerExist(AbstractExtension extension)
            => (bool) typeof(AbstractExtension)
                .GetProperty("InnerExist", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(extension);

        /// <summary>Value equality for primitives, strings and enums; runtime-type equality for anything else,
        /// which is what distinguishes a present <c>CountReader&lt;string&gt;</c> from a missing one.</summary>
        private static void AssertComparable(string what, string member, object reference, object synthesized)
        {
            if (reference == null || synthesized == null)
            {
                AssertEqual(what, member + " is present", reference != null, synthesized != null);
                return;
            }

            var type = reference.GetType();
            if (type.IsPrimitive || type.IsEnum || reference is string || reference is decimal)
            {
                AssertEqual(what, member, reference, synthesized);
                return;
            }

            AssertEqual(what, member + " runtime type", type, synthesized.GetType());
        }

        private static void AssertEqual(string what, string member, object expected, object actual)
        {
            Assert.True(Equals(expected, actual),
                what + ": the synthesized post-state differs from the engine's at " + member + " — engine had '" +
                (expected ?? "<null>") + "', Init produced '" + (actual ?? "<null>") + "'.");
        }

        private static FieldInfo Field(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.True(field != null, "Field '" + name + "' is gone from " + type.Name +
                                       "; this suite reads the engine's own post-state and must be updated with it.");
            return field;
        }

        private static IReadOnlyCollection<Type> BuiltInExtensionTypes()
        {
            var engine = typeof(AbstractExtension).Assembly;
            var attribute = engine.GetType("Heddle.Attributes.ExtensionNameAttribute");
            return engine.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(AbstractExtension).IsAssignableFrom(t) &&
                            t.GetCustomAttributes(attribute, true).Length > 0)
                .ToList();
        }

        // ---- fixtures ---------------------------------------------------------------------------------------

        private sealed class NullStrategy : IProcessStrategy
        {
            public string Execute(in Scope scope) => string.Empty;
            public void Render(in Scope scope) { }
        }

        /// <summary>Wraps a bound extension as a document root so the substitute can be rendered.</summary>
        private sealed class ExtensionStrategy : IProcessStrategy
        {
            private readonly AbstractExtension _extension;
            internal ExtensionStrategy(AbstractExtension extension) => _extension = extension;
            public string Execute(in Scope scope) => (string) _extension.ProcessData(scope);
            public void Render(in Scope scope) => _extension.RenderData(scope);
        }

        private sealed class ThrowingHookExtension : AbstractExtension
        {
            public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType,
                ExType parent) => throw new InvalidOperationException("hook says no");

            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }

        private sealed class TwoBodyExtension : AbstractExtension
        {
            public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType,
                ExType parent)
            {
                base.InitStart(initContext, dataType, chainedType, parent);
                return base.InitStart(initContext, dataType, chainedType, parent);
            }

            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }

        private sealed class NoBodyExtension : AbstractExtension
        {
            public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType,
                ExType parent) => dataType;

            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }

        private sealed class ErrorReportingExtension : AbstractExtension
        {
            public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType,
                ExType parent)
            {
                initContext.CompileScope.CompileErrors.Add(
                    "this construct is not supported here".ToError(Position));
                return base.InitStart(initContext, dataType, chainedType, parent);
            }

            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }
    }
}
