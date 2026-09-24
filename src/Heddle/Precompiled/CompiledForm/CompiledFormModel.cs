using System.Collections.Generic;
using Heddle.Precompiled;

namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>A template position: absolute start offset and length, in UTF-16 code units of the
    /// template's decoded text.</summary>
    internal sealed class CompiledPosition
    {
        public CompiledPosition(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public int Start { get; set; }

        public int Length { get; set; }
    }

    /// <summary>The artifact header: the compile's declared inputs plus the template count the writer
    /// checks against the template rows.</summary>
    internal sealed class CompiledHeader
    {
        public string EngineVersion { get; set; } = string.Empty;

        public string BuilderVersion { get; set; } = string.Empty;

        public string ExpressionMode { get; set; } = string.Empty;

        public bool TrimDirectiveLines { get; set; }

        public string DefaultOutputProfile { get; set; }
    }

    /// <summary>One extension identity: the registry name, the extension type, and the prop-layout
    /// fingerprint string when the extension declares props.</summary>
    internal sealed class CompiledExtensionRow
    {
        public string RegistryName { get; set; } = string.Empty;

        public CompiledTypeRef Type { get; set; }

        public string Fingerprint { get; set; }
    }

    /// <summary>One function binding row: the name, the bound target type, or no target for a call the
    /// build registry could not bind, plus the overload count the build saw.</summary>
    internal sealed class CompiledFunctionRow
    {
        public string Name { get; set; } = string.Empty;

        public CompiledTypeRef Target { get; set; }

        public int OverloadCount { get; set; }
    }

    /// <summary>One hop of a member path: the declaring type and member type the build bound, or neither
    /// for a hop the engine classifies dynamic.</summary>
    internal sealed class CompiledMemberHop
    {
        public CompiledTypeRef DeclaringType { get; set; }

        public string MemberName { get; set; } = string.Empty;

        public CompiledTypeRef MemberType { get; set; }
    }

    /// <summary>One member path: the start type, the segment names, and one identity per segment.</summary>
    internal sealed class CompiledMemberRow
    {
        public CompiledMemberRow()
        {
            Segments = new List<string>();
            Hops = new List<CompiledMemberHop>();
        }

        public CompiledTypeRef StartType { get; set; }

        public IList<string> Segments { get; set; }

        public IList<CompiledMemberHop> Hops { get; set; }
    }

    /// <summary>The closed native-expression node vocabulary.</summary>
    internal enum CompiledExprKind
    {
        Literal,
        This,
        Path,
        Index,
        Call,
        Unary,
        Binary,
        Ternary,
        MethodCall
    }

    /// <summary>The operators representable in a stored expression tree. Binary and unary flavors share
    /// the enum; the carrying node disambiguates them.</summary>
    internal enum CompiledExprOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        LeftShift,
        RightShift,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal,
        NotEqual,
        And,
        ExclusiveOr,
        Or,
        AndAlso,
        OrElse,
        Coalesce,
        Not,
        Negate,
        UnaryPlus,
        OnesComplement
    }

    /// <summary>The typed value of a literal node.</summary>
    internal enum CompiledLiteralKind
    {
        Null,
        Int64,
        UInt64,
        Float,
        Double,
        Decimal,
        Boolean,
        String,
        Char
    }

    /// <summary>One literal value. Only the field matching <see cref="Kind"/> is encoded.</summary>
    internal sealed class CompiledLiteral
    {
        public CompiledLiteralKind Kind { get; set; }

        public long Int64 { get; set; }

        public ulong UInt64 { get; set; }

        public float Float32 { get; set; }

        public double Double { get; set; }

        public decimal Decimal { get; set; }

        public bool Boolean { get; set; }

        public string Text { get; set; }

        public int CharCode { get; set; }
    }

    /// <summary>One node of a stored expression tree. Only the fields matching <see cref="Kind"/>
    /// are encoded.</summary>
    internal sealed class CompiledExpression
    {
        public CompiledExpression()
        {
            Segments = new List<string>();
            Arguments = new List<CompiledExpression>();
        }

        public CompiledExprKind Kind { get; set; }

        public CompiledPosition Position { get; set; }

        public CompiledLiteral Literal { get; set; }

        public bool RootRef { get; set; }

        public IList<string> Segments { get; set; }

        public CompiledExpression Target { get; set; }

        public IList<CompiledExpression> Arguments { get; set; }

        public string Name { get; set; }

        public CompiledExprOperator Operator { get; set; }

        public CompiledExpression Left { get; set; }

        public CompiledExpression Right { get; set; }

        public CompiledExpression Operand { get; set; }

        public CompiledExpression Condition { get; set; }

        public CompiledExpression WhenTrue { get; set; }

        public CompiledExpression WhenFalse { get; set; }
    }

    /// <summary>One stored expression tree: the root node, the scope types the build compiled it
    /// against, and whether it contains a call the build left for the load registry.</summary>
    internal sealed class CompiledExpressionTree
    {
        public CompiledExpression Root { get; set; }

        public CompiledTypeRef ModelType { get; set; }

        public CompiledTypeRef ChainedType { get; set; }

        public CompiledTypeRef RootType { get; set; }

        public bool ContainsDeferredCall { get; set; }
    }

    /// <summary>One embedded C# site: the source text, the namespace imports, the scope types, and
    /// the position. Carried as data; compiled through the engine's C# tier at load.</summary>
    internal sealed class CompiledCSharpSite
    {
        public CompiledCSharpSite()
        {
            Usings = new List<string>();
        }

        public string Source { get; set; } = string.Empty;

        public IList<string> Usings { get; set; }

        public CompiledTypeRef ModelType { get; set; }

        public CompiledTypeRef ChainedType { get; set; }

        public CompiledTypeRef RootType { get; set; }

        public CompiledPosition Position { get; set; }
    }

    /// <summary>The parse facts of one document: the facts the text path hands template creation and every
    /// compile the document triggers, so the loader synthesizes the same parse context.</summary>
    internal sealed class CompiledParseFacts
    {
        public CompiledParseFacts()
        {
            VisibleDefinitionRefs = new List<int>();
        }

        public int Offset { get; set; }

        public bool InDefinitionContext { get; set; }

        /// <summary>Indices into the artifact's definitions of the definitions visible from this document.</summary>
        public IList<int> VisibleDefinitionRefs { get; set; }
    }

    /// <summary>The parameter kinds a chain item can carry.</summary>
    internal enum CompiledParameterKind
    {
        None,
        Constant,
        ModelPath,
        RootPath,
        DynamicPath,
        Chain,
        NativeExpression,
        CSharpExpression,
        LateBoundCall,
        RefusalSite,
        DefinitionCall,
        PropsSlot
    }

    /// <summary>A refusal site's source: the text rebuilt at load plus the compile context the build saw.</summary>
    internal sealed class CompiledRefusalSource
    {
        public CompiledRefusalSource()
        {
            Namespaces = new List<string>();
        }

        public string SourceText { get; set; } = string.Empty;

        public CompiledTypeRef ModelType { get; set; }

        public CompiledTypeRef ChainedType { get; set; }

        public CompiledTypeRef RootType { get; set; }

        public IList<string> Namespaces { get; set; }

        public CompiledPosition Position { get; set; }

        public PrecompiledRefusalClass Class { get; set; }
    }

    /// <summary>One item parameter. Only the fields matching <see cref="Kind"/> are encoded.</summary>
    internal sealed class CompiledParameter
    {
        public CompiledParameter()
        {
            Segments = new List<string>();
        }

        public CompiledParameterKind Kind { get; set; }

        public CompiledLiteral Constant { get; set; }

        /// <summary>Index into the artifact's member rows, for model and root paths.</summary>
        public int MemberRef { get; set; }

        /// <summary>The segment names, for dynamic paths and for the rest path past a prop slot.</summary>
        public IList<string> Segments { get; set; }

        public CompiledChain NestedChain { get; set; }

        /// <summary>Index into the artifact's expression trees, for native and late-bound parameters.</summary>
        public int ExpressionRef { get; set; }

        public bool UsesPropsSlot { get; set; }

        /// <summary>Index into the artifact's site table, for embedded C# parameters.</summary>
        public int SiteRef { get; set; }

        public CompiledRefusalSource Refusal { get; set; }

        /// <summary>The prop slot index, for prop-slot reads.</summary>
        public int SlotIndex { get; set; }

        /// <summary>The hops past the slot: the full rest-path identities for a statically bound rest,
        /// the bound prefix for a dynamic rest, empty for a bare slot read.</summary>
        public IList<CompiledMemberHop> PropHops { get; set; }

        /// <summary>Whether the rest path past the slot binds dynamically.</summary>
        public bool PropDynamicRest { get; set; }

        /// <summary>Index into the artifact's definitions, for definition call sites.</summary>
        public int DefinitionRef { get; set; }

        /// <summary>Index into the artifact's documents of the caller-content document.</summary>
        public int CallerContentRef { get; set; }

        public bool SlotMode { get; set; }
    }

    /// <summary>One dynamic prop slot: the slot index, the expression computing it, and the conversion
    /// target type.</summary>
    internal sealed class CompiledDynamicSlot
    {
        public int SlotIndex { get; set; }

        public int ExpressionRef { get; set; }

        public CompiledTypeRef TargetType { get; set; }
    }

    /// <summary>An item's props: the frozen prototype (a null entry marks a dynamic slot) plus the plan
    /// for each dynamic slot.</summary>
    internal sealed class CompiledProps
    {
        public CompiledProps()
        {
            FrozenPrototype = new List<CompiledLiteral>();
            DynamicSlots = new List<CompiledDynamicSlot>();
        }

        public IList<CompiledLiteral> FrozenPrototype { get; set; }

        public IList<CompiledDynamicSlot> DynamicSlots { get; set; }
    }

    /// <summary>A body handed to an extension compile: the raw and shaped text, the data and chained types
    /// the hook handed the compile, and the compiled document when the engine compiled it to processors.</summary>
    internal sealed class CompiledBody
    {
        public string RawText { get; set; } = string.Empty;

        public string ShapedText { get; set; } = string.Empty;

        public CompiledTypeRef DataType { get; set; }

        public CompiledTypeRef ChainedType { get; set; }

        public int? CompiledDocumentRef { get; set; }
    }

    /// <summary>One chain item: the extension, the position, the return type, the parameter template text,
    /// the parameter, and the optional body, caller content and props.</summary>
    internal sealed class CompiledItem
    {
        public CompiledItem()
        {
            AltBodies = new List<CompiledAltBody>();
        }

        /// <summary>Index into the artifact's extensions.</summary>
        public int ExtensionRef { get; set; }

        public CompiledPosition Position { get; set; }

        public CompiledTypeRef ReturnType { get; set; }

        public string ParameterTemplate { get; set; }

        public CompiledParameter Parameter { get; set; }

        public CompiledBody Body { get; set; }

        /// <summary>Further bodies requested under the same position but a different template: a
        /// definition call records both its caller content (under the call's template) and its
        /// default body (under the definition's template). Served under their own templates.</summary>
        public IList<CompiledAltBody> AltBodies { get; set; }

        public CompiledProps Props { get; set; }

        /// <summary>Where in this template the <c>@&lt;&lt;</c> composition import that composed this item's
        /// file sits, or <c>-1</c> for an item the template carries itself. An import expands inline, so two
        /// imports' items land in one document carrying positions that are offsets into two different
        /// files; the loader correlates an item by position, so without this they would be one key.</summary>
        public int ImportAnchor { get; set; } = -1;
    }

    /// <summary>One alternate requested template and the body compiled for it.</summary>
    internal sealed class CompiledAltBody
    {
        public string Template { get; set; } = string.Empty;

        public CompiledBody Body { get; set; }
    }

    /// <summary>One chain: the ordered items the compiler visited.</summary>
    internal sealed class CompiledChain
    {
        public CompiledChain()
        {
            Items = new List<CompiledItem>();
        }

        public IList<CompiledItem> Items { get; set; }
    }

    /// <summary>One document element: either a static piece or a chain.</summary>
    internal sealed class CompiledElement
    {
        public bool IsChain { get; set; }

        public string StaticPiece { get; set; }

        public CompiledChain Chain { get; set; }
    }

    /// <summary>One shaped document: the shaped text, the locals flag, the parse facts, and the
    /// element list.</summary>
    internal sealed class CompiledDocument
    {
        public CompiledDocument()
        {
            Elements = new List<CompiledElement>();
            RemovedItems = new List<CompiledItem>();
        }

        public string ShapedText { get; set; } = string.Empty;

        /// <summary>The document's pre-shaping source: the exact text the build parsed. The loader
        /// re-parses this (never <see cref="ShapedText"/>): shaping is not reparseable — it collapses
        /// <c>@@</c> escapes, removes definitions and trims lines — while the raw text parses back to
        /// the same items at the same positions the build recorded. Empty for synthesized fragment
        /// documents, which the loader never parses.</summary>
        public string RawText { get; set; } = string.Empty;

        public bool NeedsLocals { get; set; }

        public CompiledParseFacts ParseFacts { get; set; }

        public IList<CompiledElement> Elements { get; set; }

        /// <summary>Items the build compiled whose elements it then removed (zero-output hooks), in
        /// position order. Carried minimally — position, template and body only — so the loader serves
        /// their bodies and runs the text path verbatim; nothing else reads them.</summary>
        public IList<CompiledItem> RemovedItems { get; set; }
    }

    /// <summary>One prop declaration of a definition: the name, the slot index and type, and the
    /// default value when one is declared.</summary>
    internal sealed class CompiledPropDecl
    {
        public string Name { get; set; } = string.Empty;

        public int SlotIndex { get; set; }

        public CompiledTypeRef SlotType { get; set; }

        public CompiledLiteral DefaultValue { get; set; }
    }

    /// <summary>One region fill: the region name and the filling document.</summary>
    internal sealed class CompiledRegionFill
    {
        public string RegionName { get; set; } = string.Empty;

        public int DocumentRef { get; set; }
    }

    /// <summary>One definition and region layout: the name, the base chain, the model type spelling and
    /// its resolved reference, the parameter template text, the prop declarations, the slot type, the
    /// region declarations and fills, and the position.</summary>
    internal sealed class CompiledDefinition
    {
        public CompiledDefinition()
        {
            PropDecls = new List<CompiledPropDecl>();
            Regions = new List<string>();
            Fills = new List<CompiledRegionFill>();
        }

        public string Name { get; set; } = string.Empty;

        public string BaseName { get; set; }

        public string ModelTypeSpelling { get; set; } = string.Empty;

        public CompiledTypeRef ModelType { get; set; }

        public string ParameterTemplate { get; set; }

        public IList<CompiledPropDecl> PropDecls { get; set; }

        public CompiledTypeRef SlotType { get; set; }

        public IList<string> Regions { get; set; }

        public IList<CompiledRegionFill> Fills { get; set; }

        public CompiledPosition Position { get; set; }
    }

    /// <summary>One template import: the key and the content hash the build saw.</summary>
    internal sealed class CompiledImport
    {
        public string Key { get; set; } = string.Empty;

        public string ContentHash { get; set; } = string.Empty;
    }

    /// <summary>One template's options fingerprint: the output profile, the expression mode, and the
    /// trim flag.</summary>
    internal sealed class CompiledOptionsFingerprint
    {
        public string Profile { get; set; }

        public string Mode { get; set; } = string.Empty;

        public bool Trim { get; set; }
    }

    /// <summary>One template row: the key, the content hash, the model type, the entry point, the
    /// imports, the fingerprint, the referenced rows, the root document, the definitions, the site
    /// count, and the refusal sites.</summary>
    internal sealed class CompiledTemplateRow
    {
        public CompiledTemplateRow()
        {
            Imports = new List<CompiledImport>();
            ExtensionRefs = new List<int>();
            FunctionRefs = new List<int>();
            DefinitionRefs = new List<int>();
            RefusalSites = new List<PrecompiledRefusalSite>();
        }

        public string Key { get; set; } = string.Empty;

        public string RegisteredName { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public CompiledTypeRef ModelType { get; set; }

        public bool ModelTypeIsAmbient { get; set; }

        public bool IsDynamic { get; set; }

        public string EntryPointTypeName { get; set; }

        /// <summary>True for a <c>Precompile="false"</c> item. The row carries the template's raw text
        /// (its root document has no elements) so every <c>@&lt;&lt;</c> that imports it replays from the
        /// artifact, and nothing else: no compile, no sites, no entry point, and the registry never
        /// serves it, so the template renders through the dynamic path.</summary>
        public bool IsImportOnly { get; set; }

        public IList<CompiledImport> Imports { get; set; }

        public CompiledOptionsFingerprint Options { get; set; }

        public IList<int> ExtensionRefs { get; set; }

        /// <summary>Indices into the artifact's function rows.</summary>
        public IList<int> FunctionRefs { get; set; }

        public int RootDocumentRef { get; set; }

        /// <summary>Indices into the artifact's definitions owned by this template.</summary>
        public IList<int> DefinitionRefs { get; set; }

        public int SiteCount { get; set; }

        public IList<PrecompiledRefusalSite> RefusalSites { get; set; }
    }

    /// <summary>The site kinds: every delegate-bearing site of a template.</summary>
    internal enum CompiledSiteKind
    {
        MemberAccessor,
        NativeExpression,
        EmbeddedCSharp,
        LateBoundCall,
        Refusal
    }

    /// <summary>One site-table row: the template index, the site ordinal in the template's fixed walk,
    /// the kind, and the payload row the kind implies (member, expression, C# site, or refusal document).</summary>
    internal sealed class CompiledSiteRow
    {
        public int TemplateIndex { get; set; }

        public int SiteOrdinal { get; set; }

        public CompiledSiteKind Kind { get; set; }

        public int PayloadRef { get; set; }
    }

    /// <summary>One compiled-form artifact: the header plus every section's rows. The string and type
    /// tables are derived by the writer in first-use order of a fixed walk, so equal models encode to
    /// identical bytes.</summary>
    internal sealed class CompiledArtifact
    {
        public CompiledArtifact()
        {
            Extensions = new List<CompiledExtensionRow>();
            Functions = new List<CompiledFunctionRow>();
            Members = new List<CompiledMemberRow>();
            Expressions = new List<CompiledExpressionTree>();
            CSharpSites = new List<CompiledCSharpSite>();
            Documents = new List<CompiledDocument>();
            Definitions = new List<CompiledDefinition>();
            Templates = new List<CompiledTemplateRow>();
            Sites = new List<CompiledSiteRow>();
        }

        public CompiledHeader Header { get; set; }

        public IList<CompiledExtensionRow> Extensions { get; set; }

        public IList<CompiledFunctionRow> Functions { get; set; }

        public IList<CompiledMemberRow> Members { get; set; }

        public IList<CompiledExpressionTree> Expressions { get; set; }

        public IList<CompiledCSharpSite> CSharpSites { get; set; }

        public IList<CompiledDocument> Documents { get; set; }

        public IList<CompiledDefinition> Definitions { get; set; }

        public IList<CompiledTemplateRow> Templates { get; set; }

        public IList<CompiledSiteRow> Sites { get; set; }

        /// <summary>Lowercase-hex SHA-256 of the artifact bytes with the digest field zeroed. Set by the
        /// writer after encoding and by the reader after verifying.</summary>
        public string DigestHex { get; set; }
    }
}
