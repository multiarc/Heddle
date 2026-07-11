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
        /// notably <c>@for(Name)</c> with a non-<c>int</c>/<c>ForModel</c> value).</summary>
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

        // Phase 9 (HED9001) is intentionally NOT a public constant here: the phase adds no public API surface
        // (see the phase 9 spec's Public API contract). Its stable code lives on the internal
        // Heddle.Runtime.HeddleFeatures.CSharpTierDisabledDiagnosticId, surfaced through HeddleCompileError.DiagnosticId.
    }
}
