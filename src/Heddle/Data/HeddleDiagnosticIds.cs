namespace Heddle.Data
{
    /// <summary>
    /// <para>Stable diagnostic-ID constants surfaced by <see cref="HeddleCompileError"/> and consumed by
    /// tooling (see the cross-cutting diagnostic-ID registry).</para>
    /// <para>IDs are allocated in per-phase blocks — <c>HED0xxx</c> for pre-existing core diagnostics and
    /// <c>HED1xxx</c> for the phase 1 native-expression tier. An ID once shipped is never reused or
    /// renumbered.</para>
    /// </summary>
    public static class HeddleDiagnosticIds
    {
        /// <summary>A member-path segment fails the shared resolver filter (missing, non-readable,
        /// <c>[Hidden]</c>, or inaccessible getter).</summary>
        public const string PropertyNotFound = "HED0001";

        /// <summary>An extension name could not be resolved by <c>TemplateFactory.Create</c>.</summary>
        public const string ExtensionNotFound = "HED0002";

        /// <summary>An ANTLR parser syntax error.</summary>
        public const string SyntaxError = "HED0003";

        /// <summary>A chained/model return type is assignable to none of an extension's declared
        /// <c>[DataType]</c>s (pre-existing <c>CheckTypes</c> message; assigned as touched in phase 4 —
        /// notably <c>@for(Name)</c> with a non-<c>int</c>/<c>Range</c> value).</summary>
        public const string ReturnTypeMismatch = "HED0004";

        /// <summary>A native-expression function name matched neither the registry nor an extension/definition.</summary>
        public const string UnknownFunction = "HED1001";

        /// <summary>A native-expression call names an extension/definition rather than a registered function.</summary>
        public const string ExtensionCalledAsFunction = "HED1002";

        /// <summary>Method-call syntax (<c>x.Foo(...)</c>) appears in a native expression.</summary>
        public const string MethodCallNotAvailable = "HED1003";

        /// <summary>A native-expression operand is a dynamic scope or crosses a <c>[Dynamic]</c> property.</summary>
        public const string TypedModelRequired = "HED1004";

        /// <summary><c>&amp;&amp;</c>/<c>||</c> applied to a non-<c>bool</c> operand.</summary>
        public const string LogicalOperatorRequiresBool = "HED1005";

        /// <summary><c>??</c> applied to a non-nullable value-type left operand.</summary>
        public const string CoalesceLeftNotNullable = "HED1006";

        /// <summary>The conditional operator arms have no common type.</summary>
        public const string TernaryArmsNoCommonType = "HED1007";

        /// <summary>A binary operator has no defined rule for its operand types, or a numeric literal overflows.</summary>
        public const string BinaryOperatorNotDefined = "HED1008";

        /// <summary>A unary operator is not defined for its operand type.</summary>
        public const string UnaryOperatorNotDefined = "HED1009";

        /// <summary>An indexer target has no accessible indexer matching the argument types.</summary>
        public const string IndexerNotFound = "HED1010";

        /// <summary>The conditional operator condition is not <c>bool</c>.</summary>
        public const string TernaryConditionNotBool = "HED1011";

        /// <summary>No overload of a registered function binds to the supplied argument types.</summary>
        public const string NoFunctionOverload = "HED1012";

        /// <summary>A registered-function call is ambiguous between two candidates.</summary>
        public const string AmbiguousFunctionCall = "HED1013";

        /// <summary>A native expression is used while <c>ExpressionMode</c> is <c>MemberPathsOnly</c>.</summary>
        public const string NativeExpressionsDisabled = "HED1014";

        /// <summary>A composite <c>format</c> literal references an argument index beyond the supplied count.</summary>
        public const string FormatArgumentCountMismatch = "HED1015";

        /// <summary>A registered function is shadowed by an extension with the same name (standalone calls).</summary>
        public const string FunctionShadowedByExtension = "HED1016";

        /// <summary>A standalone registry hit was given a chain/C# parameter shape.</summary>
        public const string FunctionRequiresExpressionArguments = "HED1017";

        /// <summary>A <c>@profile()</c> directive names a value other than <c>text</c>/<c>html</c> (or is empty).</summary>
        public const string UnknownOutputProfile = "HED2001";

        /// <summary>A <c>@profile()</c> directive appears after output has already been compiled in the same scope.</summary>
        public const string ProfileDirectiveAfterOutput = "HED2002";

        /// <summary>A nested <c>[EncodeOutput]</c> producer feeds the auto-encoding unnamed output under the Html profile.</summary>
        public const string RedundantEncodingExtension = "HED2003";

        /// <summary>A bare, bodiless unnamed <c>@(value)</c> output sits — under the Html profile — in an HTML
        /// attribute value, a <c>&lt;script&gt;</c> block, or a URL component, where element-text encoding is
        /// insufficient or wrong; the matching context encoder (<c>@attr</c>/<c>@js</c>/<c>@url</c>) is not used.
        /// Warning; heuristic (local adjacent-literal scan); never fires off the Html profile.</summary>
        public const string MissingContextEncoder = "HED2004";

        /// <summary>Non-whitespace text between the blocks of a branch set is stripped and never rendered.</summary>
        public const string BranchTextStripped = "HED3001";

        /// <summary>A branch continuation (such as <c>@elif</c>/<c>@elseif</c>) has no preceding opener in scope — it starts a new set, acting as an opener.</summary>
        public const string ElifWithoutIf = "HED3002";

        /// <summary>A branch terminal (such as <c>@else</c>) has no matching opener in scope (orphan, or a set already closed by an earlier terminal).</summary>
        public const string ElseWithoutIf = "HED3003";

        /// <summary>A branch terminal (such as <c>@else</c>) was given a condition parameter, which is ignored.</summary>
        public const string ElseConditionIgnored = "HED3004";

        /// <summary>A branch continuation/terminal extension (<c>[BranchRole]</c>) does not carry
        /// <c>[ScopeChannel]</c>, so its read of the branch state always misses at render time (R11 drift). Never
        /// raised by the built-ins, which all comply; additive to existing behavior.</summary>
        public const string BranchRoleMissingScopeChannel = "HED3005";

        /// <summary>The literal step argument of the built-in three-argument <c>range</c> is zero or negative
        /// (a non-terminating loop). The identical condition reached only at render throws
        /// <c>TemplateProcessingException</c> with the same message and no ID.</summary>
        public const string RangeStepNotPositive = "HED4001";

        /// <summary>A by-name call resolves to a definition that carries a default output (<c>-&gt; chain</c>)
        /// and is therefore rendered twice — once at document end, once at the call.</summary>
        public const string DefinitionRendersTwice = "HED4002";

        /// <summary>The legacy <c>@import()</c> include has been removed. Any <c>@import()</c> call site now
        /// produces this error (no longer a warning), positioned at the call, and the template no longer compiles.
        /// The message names the replacements: <c>@&lt;&lt;{{ path }}</c> to share definitions and layouts across
        /// files, or <c>@partial(){{ name }}</c> to embed another template's rendered output inline. Raised once
        /// per call site; <c>@&lt;&lt;</c> never raises it.</summary>
        public const string LegacyImportDirective = "HED4003";

        /// <summary>A <c>@&lt;&lt;{{ path }}</c> composition import appears nested inside a subtemplate (an
        /// <c>@if</c>/<c>@for</c> body, an output block, or a definition body) rather than at the top level of a
        /// document. Composition merges definitions and re-bases the imported file's output chains into the
        /// current document, which is only well-defined at document scope; the import is skipped and this error
        /// is raised, positioned at the <c>@&lt;&lt;</c> directive.</summary>
        public const string ComposeImportNotTopLevel = "HED4004";

        /// <summary>A literal <c>{{ identifier }}</c> / <c>{{ dotted.path }}</c> appears in body text, where it
        /// renders verbatim braces rather than interpolating (the number-one misread for authors arriving from
        /// Liquid/Jinja/Mustache/Handlebars). Warning severity; never fires inside a real <c>{{ … }}</c> body or a
        /// raw region. Suggests <c>@(identifier)</c>.</summary>
        public const string LiquidStyleInterpolationMisread = "HED4005";

        // Phase 5 — props & slots.

        /// <summary>A named argument's name is not declared by the target definition's prop layout.</summary>
        public const string UnknownProp = "HED5001";

        /// <summary>A required prop (declared without a default) is unbound after binding at a call site.</summary>
        public const string MissingRequiredProp = "HED5002";

        /// <summary>A named argument's static type does not convert to the declared prop type.</summary>
        public const string PropTypeMismatch = "HED5003";

        /// <summary>The same prop name is passed more than once in one call.</summary>
        public const string DuplicatePropArgument = "HED5004";

        /// <summary>Named arguments are passed to a call that does not resolve to a definition.</summary>
        public const string NamedArgumentsNotSupported = "HED5005";

        /// <summary>Named arguments are passed to a definition whose prop layout is empty.</summary>
        public const string DefinitionHasNoProps = "HED5006";

        /// <summary>A prop name is declared more than once within one header's prop list.</summary>
        public const string DuplicatePropDeclaration = "HED5007";

        /// <summary>A child re-declares an inherited prop with a type not assignable to the inherited type.</summary>
        public const string PropRedeclarationMismatch = "HED5008";

        /// <summary>A prop's default literal is not convertible to the declared prop type.</summary>
        public const string PropDefaultNotConvertible = "HED5009";

        /// <summary>A prop or slot type name cannot be resolved.</summary>
        public const string UnresolvedPropType = "HED5010";

        /// <summary>A prop hides a readable, visible model member of the same name (the prop wins).</summary>
        public const string PropShadowsModelMember = "HED5011";

        /// <summary><c>@out</c> is given a value where no slot parameter is declared (or outside any definition body).</summary>
        public const string SlotValueWithoutSlot = "HED5012";

        /// <summary>An empty-parameter <c>@out()</c> appears in a slot-declaring definition body.</summary>
        public const string SlotValueRequired = "HED5013";

        /// <summary>A slot value's static type is not assignable to the declared slot parameter type (or is dynamic).</summary>
        public const string SlotValueTypeMismatch = "HED5014";

        /// <summary>A prop is declared with the reserved name <c>out</c> or <c>this</c>.</summary>
        public const string ReservedPropName = "HED5015";

        /// <summary>A slot declaration (<c>id :: Type</c>) uses an identifier other than <c>out</c>.</summary>
        public const string InvalidSlotDeclaration = "HED5016";

        /// <summary>A definition header declares more than one <c>out::</c> slot parameter.</summary>
        public const string MultipleSlotDeclarations = "HED5017";

        /// <summary>A slot-mode <c>@out(expr)</c> carries a <c>{{ … }}</c> body.</summary>
        public const string SlotValueWithBody = "HED5018";

        // Phase 7 (post-2.0) — named content regions.

        /// <summary>A call-body region override (<c>&lt;name:name&gt;</c>) targets a region the callee declares
        /// <b>private</b> (a plain inner <c>&lt;name&gt;</c>, not <c>&lt;:name&gt;</c>). Raised at the compile-time
        /// fill step, positioned at the override declaration in the caller content.</summary>
        public const string RegionNotPublic = "HED5019";

        /// <summary>A component declares more than one <b>public</b> region (<c>&lt;:name&gt;</c>) with the same
        /// name. Raised at parse for the second declaration; a public region colliding with a private or
        /// document-scope <c>&lt;name&gt;</c> keeps the pre-existing id-less duplicate message.</summary>
        public const string DuplicateRegionDeclaration = "HED5020";

        // The HED7xxx block — build-time generator (HED70xx) and precompiled-runtime registration/fallback
        // (HED71xx). Generator plan phase 6 D4/D12.1: the ids already ship (as Roslyn descriptors in
        // Heddle.Generator and as PrecompiledFallbackEvent codes here), but they were the one block not
        // reflectable from this class, so no completeness test could see them. The constants are additive —
        // nothing renumbers — and they are what lets HeddleDiagnosticCatalog be gated as a bijection.

        /// <summary>An <c>AdditionalFiles</c> <c>.heddle</c> source could not be read at generation time.</summary>
        public const string BuildUnreadableFile = "HED7001";

        /// <summary>Two templates in one compilation normalize to the same key.</summary>
        public const string BuildDuplicateKey = "HED7002";

        /// <summary>Two template keys differ only by case, so ordinal lookup makes one shadow the other.</summary>
        public const string BuildCaseOnlyKeyTwin = "HED7003";

        /// <summary>Explicit <c>Key</c> item metadata is empty, or carries a <c>.</c>/<c>..</c> segment.</summary>
        public const string BuildInvalidKeyMetadata = "HED7004";

        /// <summary>A static piece contains an unpaired surrogate, so the <c>u8</c> twins are suppressed.</summary>
        public const string BuildSurrogatePiece = "HED7005";

        /// <summary>A named extension resolves to no <c>[ExtensionName]</c> type in any referenced assembly.</summary>
        public const string BuildExtensionNotBindable = "HED7006";

        /// <summary>The <c>@model</c>/<c>::</c> type name resolves in neither the compilation nor its
        /// references.</summary>
        public const string BuildUnresolvableModelType = "HED7007";

        /// <summary>The build-tier twin of <see cref="PropertyNotFound"/>: a member path does not resolve on the
        /// model type.</summary>
        public const string BuildUnresolvableMember = "HED7008";

        /// <summary>An MSBuild build-option value is unparsable.</summary>
        public const string BuildOptionParseError = "HED7009";

        /// <summary>Two template keys sanitize to one generated entry-class identifier.</summary>
        public const string BuildDuplicateSanitizedName = "HED7010";

        /// <summary>An <c>@&lt;&lt;</c> import is not among the compilation's <c>.heddle</c>
        /// <c>AdditionalFiles</c>.</summary>
        public const string BuildImportNotIncluded = "HED7011";

        /// <summary>The wrapper for a forwarded front-end <b>error</b> carrying no id of its own; an entry that
        /// has an id is forwarded under that id (phase 6 D2).</summary>
        public const string BuildForwardedError = "HED7012";

        /// <summary>The wrapper for a forwarded front-end <b>warning</b> carrying no id of its own.</summary>
        public const string BuildForwardedWarning = "HED7013";

        /// <summary>A called function is delegate-only, so it is not representable in assembly metadata and
        /// cannot be precompiled; the template renders through the dynamic path.</summary>
        public const string BuildUnresolvableFunction = "HED7014";

        /// <summary>A bound extension overrides a compile-time hook the generator cannot evaluate.</summary>
        public const string BuildExtensionOverridesHook = "HED7015";

        /// <summary>The build-tier twin of <see cref="BranchRoleMissingScopeChannel"/>.</summary>
        public const string BuildBranchRoleMissingScopeChannel = "HED7016";

        /// <summary>The build-tier twin of the declaration-side malformed-<c>[Prop]</c> diagnostics
        /// (<see cref="DuplicatePropDeclaration"/>, <see cref="PropRedeclarationMismatch"/>,
        /// <see cref="PropDefaultNotConvertible"/>, <see cref="UnresolvedPropType"/>,
        /// <see cref="ReservedPropName"/>).</summary>
        public const string BuildMalformedExtensionParameter = "HED7017";

        /// <summary>A template outside <c>HeddleTemplateRoot</c> with no explicit <c>Key</c> registers under a
        /// flattened filename key.</summary>
        public const string BuildTemplateOutsideRoot = "HED7018";

        /// <summary>The <c>Heddle</c> assembly is not visible among the compilation's references, so the manifest
        /// records the generator's own version as <c>engineVersion</c>.</summary>
        public const string BuildEngineVersionUnresolved = "HED7019";

        /// <summary>The template emitter threw — a generator defect rather than a template error.</summary>
        public const string BuildEmitterFault = "HED7020";

        /// <summary>An <c>[ExportFunctions]</c> container that is not a public static class — the runtime throws
        /// <c>ArgumentException</c> when it registers the assembly, so the build fails the same way (Q3.6's
        /// match-principle ruling) instead of silently skipping the container.</summary>
        public const string BuildIneligibleExportContainer = "HED7021";

        /// <summary>An <c>@profile(){{…}}</c> value that is neither <c>text</c> nor <c>html</c> — the build-time
        /// twin of the runtime's <see cref="UnknownOutputProfile"/> (HED2001). Phase 1 D3: the emitter used to
        /// ignore the directive and precompile output the dynamic tier refuses to compile at all.</summary>
        public const string BuildUnknownOutputProfile = "HED7022";

        /// <summary>A model/prop/slot type name several types answer to, unsettled by the template's
        /// <c>@using</c> imports — the build-time twin of the runtime's "the type name is ambigous" throw.</summary>
        public const string BuildAmbiguousTypeName = "HED7023";

        /// <summary>A call-site fill of a region the definition declares <b>private</b> — the build-time twin of
        /// the runtime's <see cref="RegionNotPublic"/> (HED5019). Phase 1 D7 / Q1.3's match principle: the
        /// generator reacts to the region-fill verdict exactly as the dynamic engine does, so the error surfaces
        /// at build instead of waiting for the first dynamic render.</summary>
        public const string BuildRegionNotPublic = "HED7024";

        /// <summary>A function call the shared overload ranker <b>proved</b> illegal — an ambiguous flat-Pareto
        /// front (<see cref="AmbiguousFunctionCall"/>, HED1013) or no applicable overload
        /// (<see cref="NoFunctionOverload"/>, HED1012) — over arguments the generator could type. Q8.1's ruling:
        /// the generator had already computed the illegality and then reported nothing, so a provably illegal
        /// template got a green build and a hard run-time error at first render. Reported only when no argument
        /// estimate is <c>Unknown</c>; an argument the estimator cannot type proves nothing and still degrades
        /// silently.</summary>
        public const string BuildFunctionCallNotBindable = "HED7025";

        /// <summary>An <c>@&lt;&lt;</c> import names a template by its registration key while that template also
        /// carries a <c>Name</c> item metadatum. Q8.25: <c>Name</c> is <b>additive</b>, so both spellings resolve and
        /// the import is not a fault — this is an advisory that the name-first spelling is the preferred one for a
        /// named template. A genuinely new fault class: every other HED70xx key diagnostic reports something
        /// unusable, and this one reports something that works.</summary>
        public const string BuildNamedTemplateImportedByKey = "HED7028";

        /// <summary>A precompiled entry failed the run-time gauntlet, so the render degrades to the dynamic
        /// tier (carried on <c>PrecompiledFallbackEvent.DiagnosticId</c>).</summary>
        public const string PrecompiledGauntletFallback = "HED7101";

        /// <summary>A precompiled manifest is rejected whole — unsupported schema version, or an engine version
        /// the runtime is not compatible with.</summary>
        public const string PrecompiledManifestRejected = "HED7102";

        /// <summary>A registry lookup missed on case alone; informational, never a failure.</summary>
        public const string PrecompiledKeyCaseMismatch = "HED7103";

        // Phase 9 (HED9001) is intentionally NOT a public constant here: the phase adds no public API surface
        // (see the phase 9 spec's Public API contract). Its stable code lives on the internal
        // Heddle.Runtime.HeddleFeatures.CSharpTierDisabledDiagnosticId, surfaced through HeddleCompileError.DiagnosticId.
    }
}
