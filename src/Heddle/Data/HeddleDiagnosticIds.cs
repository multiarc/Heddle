namespace Heddle.Data
{
    /// <summary>
    /// <para>Stable diagnostic-ID constants surfaced by <see cref="HeddleCompileError"/> and consumed by
    /// tooling.</para>
    /// <para>IDs are allocated in per-feature blocks — <c>HED0xxx</c> for core diagnostics,
    /// <c>HED1xxx</c> for the native-expression tier. An ID once shipped is never reused or
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

        /// <summary>A chained/model return type is assignable to none of an extension's declared <c>[DataType]</c>s.</summary>
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

        /// <summary>A bare <c>@(value)</c> output in an HTML attribute value, <c>&lt;script&gt;</c> block, or URL component has insufficient encoding; requires <c>@attr</c>/<c>@js</c>/<c>@url</c> (Html profile only).</summary>
        public const string MissingContextEncoder = "HED2004";

        /// <summary>Non-whitespace text between the blocks of a branch set is stripped and never rendered.</summary>
        public const string BranchTextStripped = "HED3001";

        /// <summary>A branch continuation (such as <c>@elif</c>/<c>@elseif</c>) has no preceding opener in scope — it starts a new set, acting as an opener.</summary>
        public const string ElifWithoutIf = "HED3002";

        /// <summary>A branch terminal (@else) has no matching opener in scope.</summary>
        public const string ElseWithoutIf = "HED3003";

        /// <summary>A branch terminal (such as <c>@else</c>) was given a condition parameter, which is ignored.</summary>
        public const string ElseConditionIgnored = "HED3004";

        /// <summary>A branch continuation/terminal extension (<c>[BranchRole]</c>) lacks <c>[ScopeChannel]</c>, causing branch state read misses at render time (R11 drift).</summary>
        public const string BranchRoleMissingScopeChannel = "HED3005";

        /// <summary>The literal step argument of the built-in <c>range</c> is zero or negative (non-terminating loop).</summary>
        public const string RangeStepNotPositive = "HED4001";

        /// <summary>A by-name call resolves to a definition that carries a default output (<c>-&gt; chain</c>)
        /// and is therefore rendered twice — once at document end, once at the call.</summary>
        public const string DefinitionRendersTwice = "HED4002";

        /// <summary><c>@import()</c> is removed; use <c>@&lt;&lt;{{ path }}</c> for composition or <c>@partial(){{ name }}</c> for embedding.</summary>
        public const string LegacyImportDirective = "HED4003";

        /// <summary>Composition import <c>@&lt;&lt;{{ path }}</c> must be at document scope, not nested in <c>@if</c>/<c>@for</c> bodies, outputs, or definitions.</summary>
        public const string ComposeImportNotTopLevel = "HED4004";

        /// <summary>A literal <c>{{ identifier }}</c> in body text renders verbatim braces; use <c>@(identifier)</c> to interpolate (warning only).</summary>
        public const string LiquidStyleInterpolationMisread = "HED4005";

        /// <summary>An <c>@&lt;&lt;</c> composition import reaches a document already being imported; the cycle is
        /// reported and the repeat import skipped.</summary>
        public const string ComposeImportCycle = "HED4006";

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

        /// <summary>A call-body region override targets a region the callee declared private.</summary>
        public const string RegionNotPublic = "HED5019";

        /// <summary>A component declares more than one public region with the same name.</summary>
        public const string DuplicateRegionDeclaration = "HED5020";

        // HED7xxx: build-time generator (HED70xx) and precompiled-runtime (HED71xx) ids shipped in Roslyn descriptors
        // and PrecompiledFallbackEvent. Additive; essential for HeddleDiagnosticCatalog bijection.

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
        /// has an id is forwarded under that id.</summary>
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

        /// <summary>An <c>[ExportFunctions]</c> container that is not a public static class (runtime throws <c>ArgumentException</c>).</summary>
        public const string BuildIneligibleExportContainer = "HED7021";

        /// <summary>An <c>@profile(){{…}}</c> value that is neither <c>text</c> nor <c>html</c> (build-time twin of <see cref="UnknownOutputProfile"/>, HED2001).</summary>
        public const string BuildUnknownOutputProfile = "HED7022";

        /// <summary>A model/prop/slot type name several types answer to, unsettled by the template's
        /// <c>@using</c> imports — the build-time twin of the runtime's "the type name is ambigous" throw.</summary>
        public const string BuildAmbiguousTypeName = "HED7023";

        /// <summary>A call-site fill of a region the definition declares <b>private</b> — the build-time twin of
        /// the runtime's <see cref="RegionNotPublic"/> (HED5019). The generator reacts to the region-fill
        /// verdict exactly as the dynamic engine does, so the error surfaces
        /// at build instead of waiting for the first dynamic render.</summary>
        public const string BuildRegionNotPublic = "HED7024";

        /// <summary>A function call the generator proved illegal (ambiguous overload or no applicable match) over typeable arguments; reported at build time only when argument types are certain.</summary>
        public const string BuildFunctionCallNotBindable = "HED7025";

        /// <summary>An <c>@&lt;&lt;</c> import uses the template's registration key when a <c>Name</c> metadatum exists (both spellings work; advisory to prefer <c>Name</c>).</summary>
        public const string BuildNamedTemplateImportedByKey = "HED7028";

        /// <summary>A precompiled entry failed the run-time gauntlet, so the render degrades to the dynamic
        /// tier (carried on <c>PrecompiledFallbackEvent.DiagnosticId</c>).</summary>
        public const string PrecompiledGauntletFallback = "HED7101";

        /// <summary>A precompiled manifest is rejected whole — unsupported schema version, or an engine version
        /// the runtime is not compatible with.</summary>
        public const string PrecompiledManifestRejected = "HED7102";

        /// <summary>A registry lookup missed on case alone; informational, never a failure.</summary>
        public const string PrecompiledKeyCaseMismatch = "HED7103";

        /// <summary>A template's registered <c>Name</c> collides with another template's key or name (runtime id: cross-assembly collision detected at registration; key remains usable).</summary>
        public const string PrecompiledRegisteredNameUnavailable = "HED7104";

        // HED9001 intentionally absent: C# expression tier has no public API surface; defined in internal
        // Heddle.Runtime.HeddleFeatures.CSharpTierDisabledDiagnosticId.
    }
}
