using System.Collections.Generic;

namespace Heddle.Data
{
    /// <summary>
    /// One prop-declaration fault class. Declaration order <b>is</b> the runtime's validation order
    /// (see <c>HeddleDiagnosticCatalog.PropFaults.FaultOrder</c>); the shared <c>PropLayoutCore</c> is the only
    /// evaluator, so both tiers report the same fault for the same declaration in the same sequence.
    /// </summary>
    internal enum PropFault
    {
        /// <summary>Null / empty / whitespace name.</summary>
        NameInvalid = 0,

        /// <summary>A reserved call-shape keyword (<c>out</c>, <c>this</c>).</summary>
        NameReserved = 1,

        /// <summary>The same name declared twice at one inheritance level.</summary>
        DuplicateAtLevel = 2,

        /// <summary>Null / open generic / pointer / by-ref declared type.</summary>
        TypeUnusable = 3,

        /// <summary>An inherited re-declaration whose type is not assignable to the inherited one.</summary>
        RedeclarationNotAssignable = 4,

        /// <summary>The declared default cannot be converted to the declared type (including an illegal
        /// null default on a non-nullable value type).</summary>
        DefaultNotConvertible = 5
    }

    /// <summary>Default severity of a diagnostic ID, independent of the surface reporting it. A Heddle
    /// <i>compile</i> diagnostic is an error or a warning and nothing else — <see cref="Info"/> exists for the one
    /// thing that is neither, a note about the BUILD rather than about a template, and adding it was a deliberate
    /// registry change rather than a table change.</summary>
    internal enum HeddleDiagnosticSeverity
    {
        Error,
        Warning,

        /// <summary>Neither a template fault nor a warning about one: a note about what the build could and could
        /// not do. Never raised by the engine's own compile channels.</summary>
        Info
    }

    /// <summary>One registry row: the shared identity of a <c>HED</c> diagnostic.
    /// <see cref="MessageFormat"/> is <c>null</c> for rows whose message text is owned by a single raise
    /// site — no second copy of prose is created where no second consumer formats through it.</summary>
    internal readonly struct HeddleDiagnosticInfo
    {
        public HeddleDiagnosticInfo(string id, string title, HeddleDiagnosticSeverity defaultSeverity,
            string messageFormat = null)
        {
            Id = id;
            Title = title;
            DefaultSeverity = defaultSeverity;
            MessageFormat = messageFormat;
        }

        /// <summary>The stable <c>HEDxxxx</c> id.</summary>
        public string Id { get; }

        /// <summary>Short noun phrase naming the fault — what a Roslyn descriptor or an editor listing shows
        /// beside the id.</summary>
        public string Title { get; }

        /// <summary>The severity the id carries wherever it is reported. Severity is a property of the
        /// diagnostic, not of the surface, so it is stated here once.</summary>
        public HeddleDiagnosticSeverity DefaultSeverity { get; }

        /// <summary>Composite format string (<c>{0}</c>…<c>{n-1}</c>), or <c>null</c> when the message text has
        /// exactly one owner — its raise site.</summary>
        public string MessageFormat { get; }
    }

    /// <summary>
    /// <para>The id → row table: the single <b>code-side</b> registry of Heddle diagnostic identity. Before it,
    /// id → (title, severity, message) was maintained in four unsynchronized places — the raise sites here,
    /// the generator's Roslyn <c>DiagnosticDescriptor</c>s, and two documentation tables — and had already
    /// drifted.</para>
    /// <para>Pure data over <see cref="string"/> and a two-value enum: no Roslyn, no dependencies, netstandard2.0,
    /// so the build host formats from this table through <c>MessageFormat</c> rather than carrying a second copy.
    /// The documentation-side registries are gated against this table by <c>DiagnosticIdTests</c> and the registry
    /// lockstep test.</para>
    /// <para><b>Content authority.</b> For a runtime-raised id the raise site is authoritative for severity;
    /// <see cref="HeddleDiagnosticInfo.MessageFormat"/> stays <c>null</c> there, because the raise site is also
    /// its message's only owner. For the <c>HED7xxx</c> block — which the runtime never raises — the build host's
    /// projection is authoritative, and those rows do carry the format: the projection is
    /// the second consumer that makes the knowledge shared rather than duplicated.</para>
    /// </summary>
    internal static class HeddleDiagnosticCatalog
    {
        /// <summary>Vocabulary the <c>[Prop]</c> declaration validators consume, so the rule exists once
        /// rather than as matching literals in the runtime's <c>PropLayout</c> and the parser's def-header
        /// pre-check.</summary>
        public static class PropFaults
        {
            /// <summary>Names a <c>[Prop]</c>/def-header parameter may not take: they are the call-shape
            /// keywords (<c>@out</c>, and <c>this</c> for the chained value).</summary>
            public static readonly string[] ReservedNames = { "out", "this" };

            /// <summary>
            /// The fault classes a prop-declaration list can produce, <b>in the runtime's validation
            /// order</b> — the order <c>PropLayout.ResolveFromExtension</c> applies them in per declaration.
            /// <para>The shared <c>PropLayoutCore</c> evaluates the checks in exactly this sequence on both tiers,
            /// so "same fault, same declaration, same order" is a property the lockstep test can assert rather
            /// than a coincidence of two hand-kept validators.</para>
            /// </summary>
            public static readonly PropFault[] FaultOrder =
            {
                PropFault.NameInvalid,
                PropFault.NameReserved,
                PropFault.DuplicateAtLevel,
                PropFault.TypeUnusable,
                PropFault.RedeclarationNotAssignable,
                PropFault.DefaultNotConvertible
            };

            /// <summary>The dynamic tier's diagnostic id for a fault class. The build tier maps every fault to the
            /// single <c>HED7017</c> row (its message names the fault), which is why the two vocabularies needed
            /// unifying rather than re-numbering.</summary>
            public static string RuntimeDiagnosticId(PropFault fault)
            {
                switch (fault)
                {
                    case PropFault.NameInvalid:
                    case PropFault.NameReserved:
                        return HeddleDiagnosticIds.ReservedPropName;
                    case PropFault.DuplicateAtLevel:
                        return HeddleDiagnosticIds.DuplicatePropDeclaration;
                    case PropFault.TypeUnusable:
                        return HeddleDiagnosticIds.UnresolvedPropType;
                    case PropFault.RedeclarationNotAssignable:
                        return HeddleDiagnosticIds.PropRedeclarationMismatch;
                    default:
                        return HeddleDiagnosticIds.PropDefaultNotConvertible;
                }
            }

            /// <summary>
            /// The <b>one</b> fault sentence both tiers quote. The dynamic tier used to
            /// spell these five conditions one way (<c>HED5007</c>/<c>HED5008</c>/<c>HED5009</c>/<c>HED5010</c>/
            /// <c>HED5015</c>) and the build tier another (<c>HED7017</c>'s fault fragment), for the same rule
            /// evaluated by the same shared core. <paramref name="owner"/> is the complete owner noun phrase
            /// (<c>extension '&lt;name&gt;'</c>); <paramref name="typeDisplay"/> and
            /// <paramref name="relatedDisplay"/> are the adapter-rendered type spellings the fault refers to.
            /// </summary>
            public static string Message(PropFault fault, string name, string owner, string typeDisplay,
                string relatedDisplay)
            {
                switch (fault)
                {
                    case PropFault.NameInvalid:
                        return $"A [Prop] parameter name on {owner} is null or empty.";
                    case PropFault.NameReserved:
                        return $"'{name}' is reserved and cannot be used as a prop name.";
                    case PropFault.DuplicateAtLevel:
                        return $"Prop '{name}' is declared more than once on {owner}.";
                    case PropFault.TypeUnusable:
                        return $"Cannot resolve type for prop '{name}' of {owner}.";
                    case PropFault.RedeclarationNotAssignable:
                        return $"Prop '{name}' is re-declared with type {typeDisplay}, which is not assignable to " +
                               $"the inherited type {relatedDisplay}.";
                    default:
                        return $"The default value for prop '{name}' ({relatedDisplay}) is not convertible to " +
                               $"{typeDisplay}.";
                }
            }

            /// <summary>Whether <paramref name="name"/> is reserved.</summary>
            public static bool IsReserved(string name)
            {
                for (var i = 0; i < ReservedNames.Length; i++)
                {
                    if (string.Equals(ReservedNames[i], name, System.StringComparison.Ordinal))
                        return true;
                }

                return false;
            }
        }

        private static readonly Dictionary<string, HeddleDiagnosticInfo> Rows = BuildRows();

        /// <summary>Every row, for enumeration by the completeness and round-trip gates.</summary>
        public static IReadOnlyCollection<HeddleDiagnosticInfo> All => Rows.Values;

        /// <summary>Looks a row up by id; <c>false</c> when the id is not catalogued.</summary>
        public static bool TryGet(string id, out HeddleDiagnosticInfo info) =>
            Rows.TryGetValue(id ?? string.Empty, out info);

        private static Dictionary<string, HeddleDiagnosticInfo> BuildRows()
        {
            var rows = new Dictionary<string, HeddleDiagnosticInfo>(System.StringComparer.Ordinal);

            void Add(string id, string title, HeddleDiagnosticSeverity severity, string format = null) =>
                rows.Add(id, new HeddleDiagnosticInfo(id, title, severity, format));

            const HeddleDiagnosticSeverity error = HeddleDiagnosticSeverity.Error;
            const HeddleDiagnosticSeverity warning = HeddleDiagnosticSeverity.Warning;
            const HeddleDiagnosticSeverity info = HeddleDiagnosticSeverity.Info;

            Add(HeddleDiagnosticIds.PropertyNotFound, "Unresolvable member path", error);
            Add(HeddleDiagnosticIds.ExtensionNotFound, "Extension not found", error);
            Add(HeddleDiagnosticIds.SyntaxError, "Template syntax error", error);
            Add(HeddleDiagnosticIds.ReturnTypeMismatch, "Chained value type mismatch", error);
            Add(HeddleDiagnosticIds.CompilationFailed, "Compilation failed", error);

            Add(HeddleDiagnosticIds.UnknownFunction, "Unknown function", error);
            Add(HeddleDiagnosticIds.ExtensionCalledAsFunction, "Extension called as a function", error);
            Add(HeddleDiagnosticIds.MethodCallNotAvailable, "Method call in a native expression", error);
            Add(HeddleDiagnosticIds.TypedModelRequired, "Typed model required", error);
            Add(HeddleDiagnosticIds.LogicalOperatorRequiresBool, "Logical operator requires bool", error);
            Add(HeddleDiagnosticIds.CoalesceLeftNotNullable, "Coalesce left operand is not nullable", error);
            Add(HeddleDiagnosticIds.TernaryArmsNoCommonType, "Conditional arms have no common type", error);
            Add(HeddleDiagnosticIds.BinaryOperatorNotDefined, "Binary operator not defined", error);
            Add(HeddleDiagnosticIds.UnaryOperatorNotDefined, "Unary operator not defined", error);
            Add(HeddleDiagnosticIds.IndexerNotFound, "Indexer not found", error);
            Add(HeddleDiagnosticIds.TernaryConditionNotBool, "Conditional condition is not bool", error);
            Add(HeddleDiagnosticIds.NoFunctionOverload, "No matching function overload", error);
            Add(HeddleDiagnosticIds.AmbiguousFunctionCall, "Ambiguous function call", error);
            Add(HeddleDiagnosticIds.NativeExpressionsDisabled, "Native expressions disabled", error);
            Add(HeddleDiagnosticIds.FormatArgumentCountMismatch, "Format argument count mismatch", error);
            Add(HeddleDiagnosticIds.FunctionShadowedByExtension, "Function shadowed by an extension", warning);
            Add(HeddleDiagnosticIds.FunctionRequiresExpressionArguments, "Function requires expression arguments",
                error);
            // Carries MessageFormat because the id has TWO formatting consumers: the engine's expression
            // compiler and the build host, which forwards the same id rather than minting a HED7xxx twin —
            // same fact, same id, same sentence on both tiers.
            Add(HeddleDiagnosticIds.DivisionByConstantZero, "Division by constant zero", error,
                "Operator '{0}' has a constant zero divisor; the expression can only throw when rendered, so it " +
                "is refused at compile time.");

            Add(HeddleDiagnosticIds.UnknownOutputProfile, "Unknown output profile", error);
            Add(HeddleDiagnosticIds.ProfileDirectiveAfterOutput, "Profile directive after output", warning);
            Add(HeddleDiagnosticIds.RedundantEncodingExtension, "Redundant encoding extension", warning);
            Add(HeddleDiagnosticIds.MissingContextEncoder, "Missing HTML-context encoder", warning);

            Add(HeddleDiagnosticIds.BranchTextStripped, "Text between branch blocks is never rendered", warning);
            Add(HeddleDiagnosticIds.ElifWithoutIf, "Branch continuation without an opener", warning);
            Add(HeddleDiagnosticIds.ElseWithoutIf, "Branch terminal without an opener", error);
            Add(HeddleDiagnosticIds.ElseConditionIgnored, "Branch terminal condition ignored", warning);
            Add(HeddleDiagnosticIds.BranchRoleMissingScopeChannel, "Branch role without scope channel", warning);

            Add(HeddleDiagnosticIds.RangeStepNotPositive, "Range step is not positive", error);
            Add(HeddleDiagnosticIds.DefinitionRendersTwice, "Definition renders twice", warning);
            Add(HeddleDiagnosticIds.LegacyImportDirective, "Removed @import directive", error);
            Add(HeddleDiagnosticIds.ComposeImportNotTopLevel, "Composition import is not top level", error);
            Add(HeddleDiagnosticIds.LiquidStyleInterpolationMisread, "Liquid-style interpolation misread", warning);
            Add(HeddleDiagnosticIds.ComposeImportCycle, "Composition import cycle", error);
            Add(HeddleDiagnosticIds.TemplateNestedTooDeeply, "Template nested too deeply", error);
            Add(HeddleDiagnosticIds.ComposeImportFanOut, "Composition import fan-out too large", error);
            Add(HeddleDiagnosticIds.ComposeImportUnreadable, "Composition import cannot be read", error);

            Add(HeddleDiagnosticIds.UnknownProp, "Unknown prop", error);
            Add(HeddleDiagnosticIds.MissingRequiredProp, "Missing required prop", error);
            Add(HeddleDiagnosticIds.PropTypeMismatch, "Prop type mismatch", error);
            Add(HeddleDiagnosticIds.DuplicatePropArgument, "Duplicate prop argument", error);
            Add(HeddleDiagnosticIds.NamedArgumentsNotSupported, "Named arguments not supported", error);
            Add(HeddleDiagnosticIds.DefinitionHasNoProps, "Definition declares no props", error);
            Add(HeddleDiagnosticIds.DuplicatePropDeclaration, "Duplicate [Prop] declaration", error);
            Add(HeddleDiagnosticIds.PropRedeclarationMismatch, "Prop re-declaration type mismatch", error);
            Add(HeddleDiagnosticIds.PropDefaultNotConvertible, "Prop default is not convertible", error);
            Add(HeddleDiagnosticIds.UnresolvedPropType, "Unresolvable prop type", error);
            Add(HeddleDiagnosticIds.PropShadowsModelMember, "Prop shadows a model member", warning);
            Add(HeddleDiagnosticIds.SlotValueWithoutSlot, "Slot value without a slot", error);
            Add(HeddleDiagnosticIds.SlotValueRequired, "Slot value required", error);
            Add(HeddleDiagnosticIds.SlotValueTypeMismatch, "Slot value type mismatch", error);
            Add(HeddleDiagnosticIds.ReservedPropName, "Reserved prop name", error);
            Add(HeddleDiagnosticIds.InvalidSlotDeclaration, "Invalid slot declaration", error);
            Add(HeddleDiagnosticIds.MultipleSlotDeclarations, "Multiple slot declarations", error);
            Add(HeddleDiagnosticIds.SlotValueWithBody, "Slot value with a body", error);
            Add(HeddleDiagnosticIds.RegionNotPublic, "Region is not public", error);
            Add(HeddleDiagnosticIds.DuplicateRegionDeclaration, "Duplicate region declaration", error);

            // These rows carry MessageFormat: the build host's projection formats them, so the format is
            // shared knowledge rather than a second copy.
            Add(HeddleDiagnosticIds.BuildUnreadableFile, "Unreadable Heddle template", error,
                "Heddle template '{0}' could not be read: {1}");
            Add(HeddleDiagnosticIds.BuildDuplicateKey, "Duplicate Heddle template key", error,
                "Duplicate Heddle template key '{0}': '{1}' and '{2}' normalize to the same key. Set an explicit " +
                "Key metadata on one, or exclude it from pre-compilation.");
            Add(HeddleDiagnosticIds.BuildCaseOnlyKeyTwin, "Case-only template key twin", warning,
                "Templates '{0}' and '{1}' differ only by case; ordinal-case-sensitive keys make one shadow the " +
                "other");
            // The message is deliberately general: the fault class is "this item's explicit key metadata is
            // unusable", and the metadata is spellable two ways (Key, Name) with a third instance of the same fault — two
            // spellings naming two different keys. One id, one call site; {1} names the offending metadata and says
            // why, so a new spelling or a new reason needs no new descriptor.
            Add(HeddleDiagnosticIds.BuildInvalidKeyMetadata, "Invalid Heddle template key metadata", error,
                "Invalid Heddle template key metadata on '{0}': {1}");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildSurrogatePiece, "Unpaired surrogate in static text", warning,
                "Static text in '{0}' contains an unpaired surrogate; UTF-8 pre-encoded pieces are disabled for " +
                "this template. Byte-sink renders will transcode at run time.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildExtensionNotBindable, "Extension not bindable", error,
                "Cannot find extension <{0}> in the referenced assemblies. Reference the assembly that defines " +
                "it, or correct the name.");
            // Retired as a generator-issued diagnostic; the build host still raises the same fact.
            Add(HeddleDiagnosticIds.BuildUnresolvableModelType, "Unresolvable model type", error,
                "Model type '{0}' is not defined in this compilation or its references");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildUnresolvableMember, "Unresolvable member path", error,
                "'{0}' does not contain an accessible member '{1}' (member path '{2}')");
            Add(HeddleDiagnosticIds.BuildOptionParseError, "Invalid Heddle build option", error,
                "Invalid value '{0}' for build option '{1}'; expected {2}");
            Add(HeddleDiagnosticIds.BuildDuplicateSanitizedName, "Duplicate generated entry-class name", error,
                "Template '{1}' sanitizes to the entry-class identifier '{2}', which {0} already uses. Rename the " +
                "file, or give the item a Key that sanitizes differently");
            // Retired in place (2026-09-17): the host reads an import outside the item set from disk, mirroring
            // the engine's ImportMap, so this fact never fires; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildImportNotIncluded, "Heddle import not included in compilation", error,
                "Import '{0}' is not included in this compilation. Add it as a <HeddleTemplate> item (use " +
                "Precompile=\"false\" for import-only files), or correct the spelling: an import is matched " +
                "against the item's key, which is case-sensitive, separated by '/', and cannot reach above the " +
                "template root. An import path is not a template key — write the extension, and do not spell it " +
                "'~/name' or '/name'.");
            Add(HeddleDiagnosticIds.BuildForwardedError, "Heddle template error", error, "{0}");
            Add(HeddleDiagnosticIds.BuildForwardedWarning, "Heddle template warning", warning, "{0}");
            Add(HeddleDiagnosticIds.BuildUnresolvableFunction, "Unresolvable function in precompiled template",
                warning,
                "Function '{0}' is neither a default built-in nor exported by a referenced assembly, and this " +
                "call cannot be bound at first render either: an argument's static type has no build-time answer, " +
                "so there is nothing to select an overload against. Export it with [ExportFunctions] on a public " +
                "static container, or give the argument a type the build can see; otherwise this template renders " +
                "through the dynamic path at run time.");
            Add(HeddleDiagnosticIds.BuildTemplateNotPrecompiled, "Template not fully precompiled", info,
                "This template is not fully precompiled: {0}. The sites named rebuild at load or render through " +
                "the dynamic path; the output is identical either way — the two tiers are parity-checked. A " +
                "strict host (PrecompiledStrictLoad) refuses such a template at load; " +
                "see the ids beside this one for the remedy each site kind names.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildExtensionOverridesHook, "Extension overrides a compile-time hook", warning,
                "Extension <{0}> ({1}) overrides {2}, compile-time logic this build has not read; precompiled " +
                "binding would silently skip it, so this template renders through the dynamic path at run time " +
                "instead. The output is identical either way. To pre-compile it, keep the extension's " +
                "compile-time behavior in the base implementation.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildBranchRoleMissingScopeChannel, "Branch role without scope channel",
                warning,
                "Branch continuation/terminal '{0}' does not carry [ScopeChannel]. It cannot read the branch " +
                "state at render time.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildMalformedExtensionParameter, "Malformed extension parameter", error,
                "Extension <{0}> declares a malformed [Prop] parameter: {1}");
            Add(HeddleDiagnosticIds.BuildTemplateOutsideRoot, "Heddle template outside the template root", warning,
                "Heddle template '{0}' is not under the template root '{1}', so its directory is dropped and it " +
                "registers under the flattened key '{2}'. Set HeddleTemplateRoot to a directory containing it, or " +
                "give the item an explicit Key metadata.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildEngineVersionUnresolved, "Heddle engine version could not be resolved",
                warning,
                "The Heddle engine assembly is not visible among this compilation's references, so the manifest " +
                "records the generator's own version '{0}' as engineVersion. Reference the Heddle package " +
                "directly (without an extern alias) if the runtime rejects the manifest with HED7102.");
            Add(HeddleDiagnosticIds.BuildIneligibleExportContainer, "Ineligible [ExportFunctions] container",
                error,
                "{0} The container exports no functions, and Heddle's runtime registry throws when the host " +
                "assembly is registered. Make the container a public static class, or drop it from the " +
                "[assembly: ExportFunctions(...)] list.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildUnknownOutputProfile, "Unknown output profile", error,
                "Unknown output profile '{0}'. Valid values: text, html. The Heddle runtime rejects this template " +
                "with HED2001, so the build reports it here rather than pre-compiling output the dynamic tier " +
                "would never produce.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildAmbiguousTypeName, "Ambiguous type name", error,
                "The type name '{0}' is ambiguous — more than one imported namespace declares it. Qualify it, or " +
                "remove one of the @using imports. The Heddle runtime raises the same ambiguity when it resolves " +
                "this name.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildRegionNotPublic, "Region is not public", error,
                "Region '{0}' of definition '{1}' is private and cannot be overridden from a call site. Mark it " +
                "public with '<:{0}>' in the definition, or remove this override. The Heddle runtime raises the " +
                "same error (HED5019) when it compiles this template.");
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildFunctionCallNotBindable, "Function call cannot be bound", error,
                "{0} The Heddle runtime rejects the same call with {1} when it compiles this template, so the " +
                "build reports it here rather than pre-compiling a call it has already proved illegal. Cast an " +
                "argument to one candidate's parameter type, or change the argument list to match one candidate.");
            // Unlike HED7004 — "this item's explicit key metadata is unusable" — nothing here is unusable:
            // the import resolved. The advice is about which of two working spellings to prefer, so it is its own
            // fault class at its own severity and needs its own id.
            Add(HeddleDiagnosticIds.BuildNamedTemplateImportedByKey,
                "Named Heddle template imported by key rather than by its registered name", warning,
                "Import '{0}' resolves a template that has a registered Name '{1}'. Both spellings resolve; prefer " +
                "'{1}' for a template with a registered name.");
            // One id for a model type, a member, and a bound extension's own type, and for accessibility and
            // error-obsolescence alike, because from the consumer's side they are one situation with one shape:
            // something the engine reads by reflection is spelled in a way this assembly's compiler rejects.
            // Splitting them would say the same sentence at several ids.
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildInaccessibleModelSymbol,
                "Symbol cannot be named by generated code", warning,
                "'{0}' cannot be named by code generated into this compilation, so this template cannot be " +
                "precompiled and renders through the dynamic path at run time. The engine binds it by reflection, " +
                "which ignores both assembly boundaries and [Obsolete], so the rendered output is unchanged. Make " +
                "it public or grant this assembly access with [InternalsVisibleTo] if it is internal, or drop the " +
                "[Obsolete(..., error: true)] if it carries one, to precompile the template.");
            Add(HeddleDiagnosticIds.BuildConflictingModelTypeDeclarations,
                "Conflicting model type declarations", error,
                "The ModelType item metadata names '{0}' while the template's @model directive names '{1}', and " +
                "they resolve to different types ('{2}' and '{3}'). The runtime reads only the directive, so the " +
                "build refuses to pick one — make the two declarations name the same type, or remove one of them.");
            // One call site, not the template: the declaration is the extension author saying which seam does not
            // serve them, and the honest cost of that is the call that uses it.
            Add(HeddleDiagnosticIds.BuildExtensionPrecompileUnsupported,
                "Extension declares its compile-time behavior cannot be precompiled", warning,
                "Extension <{0}> ({1}) declares [PrecompileUnsupported]: {2}. This call renders through the " +
                "dynamic path at run time while the rest of the template stays precompiled. The output is " +
                "identical either way.");
            // Observation is an optimisation, so its absence is news rather than a fault: the template still
            // precompiles, through the engine's own accessors, and renders the same bytes either way.
            // Retired in place: no build raises this id; the row stays so the id is never reused.
            Add(HeddleDiagnosticIds.BuildEngineNotObserved,
                "Heddle build could not observe a real engine compile", info,
                "Heddle could not observe a real engine compile ({0}). Bodies whose model type only an extension's " +
                "hook can supply are emitted type-agnostically; the template still precompiles and renders " +
                "identical output. Set <HeddleObserveEngine>Strict</HeddleObserveEngine> to make this an error, or " +
                "Off to stop trying.");
            Add(HeddleDiagnosticIds.BuildEngineVersionMismatch, "Heddle.Build engine differs from referenced Heddle",
                error,
                "Heddle.Build {0} compiles with Heddle {1} but the project references Heddle {2}; reference the " +
                "same version of both packages.");
            Add(HeddleDiagnosticIds.BuildImplementationImageNotLoaded, "Implementation assembly could not be loaded",
                error,
                "Implementation assembly '{0}' could not be loaded: {1}. Templates naming its types cannot be " +
                "compiled; fix the reference or exclude the templates with Precompile=\"false\".");
            Add(HeddleDiagnosticIds.BuildRetiredPropertySet, "Retired MSBuild property is set", warning,
                "The MSBuild property '{0}' is retired and ignored; delete it from the project.");
            Add(HeddleDiagnosticIds.BuildIntermediateCompileFailed, "Heddle intermediate model compile failed", error,
                "Heddle's intermediate model compile failed; the errors above are its compiler errors. It compiles the project's own sources once, ahead of the project's compile, because a template names a model type this project declares — so the project's own compile was not reached. Fix the errors above; or set Precompile=\"false\" on every template that binds such a model, in its own @model or through a library it imports, which then render through the dynamic path and need no model at build time; or declare the model types in a referenced project.");
            Add(HeddleDiagnosticIds.BuildEmitterFault, "Heddle build host fault", error,
                "The Heddle build host failed on '{0}': {1}: {2}. Compiling one template, this is a host defect " +
                "rather than a template error — that template emits nothing and the pass continues; please " +
                "report it, and set Precompile=\"false\" on the item to unblock the build meanwhile (the " +
                "template then renders through the dynamic path). Writing the outputs after every template " +
                "compiled, no artifact is written and the build fails.");

            // Carried on PrecompiledFallbackEvent rather than formatted, so title + severity only.
            Add(HeddleDiagnosticIds.PrecompiledGauntletFallback, "Precompiled template fell back to the dynamic tier",
                warning);
            Add(HeddleDiagnosticIds.PrecompiledManifestRejected, "Precompiled manifest rejected", warning);
            Add(HeddleDiagnosticIds.PrecompiledKeyCaseMismatch, "Precompiled key differs only by case", warning);
            Add(HeddleDiagnosticIds.PrecompiledRegisteredNameUnavailable,
                "Precompiled template's registered name is unavailable", warning);

            return rows;
        }
    }
}
