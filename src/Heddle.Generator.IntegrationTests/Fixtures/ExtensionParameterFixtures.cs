using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

// Phase 8 (WI9) — extension-parameter fixtures. Exported to the dynamic backend via the combined
// [assembly: ExportExtensions(...)] list in BranchRoleExtensions.cs (single assembly-level list; the malformed
// fixtures join it too so the DIFFERENTIAL dynamic side resolves the names — the generator resolves them by
// [ExtensionName] regardless).

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>The canonical parameter-declaring extension: one optional int parameter (default 3), read at
    /// render via <c>Scope.GetParameter</c>. No compile-time hook — binds on both tiers.</summary>
    [ExtensionName("grid")]
    [Prop("columns", typeof(int), Default = 3)]
    public sealed class GridExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            var columns = (int) scope.GetParameter("columns");
            return "cols=" + columns + ":" + (scope.ModelData?.ToString() ?? string.Empty);
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>The required-parameter variant: <c>span</c> has no default, so an omitting call is HED5002.</summary>
    [ExtensionName("gridReq")]
    [Prop("span", typeof(int))]
    public sealed class GridReqExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            var span = (int) scope.GetParameter("span");
            return "span=" + span + ":" + (scope.ModelData?.ToString() ?? string.Empty);
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>The F6/H1 fixture: <c>[EncodeOutput]</c> AND a <c>[Prop]</c> — the carrier must stay
    /// attribute-transparent so the inner still self-encodes on both tiers (an XSS-class guard). Emits
    /// markup-significant characters so Encode vs Raw differ in bytes.</summary>
    [ExtensionName("encodedGrid")]
    [EncodeOutput]
    [Prop("columns", typeof(int), Default = 3)]
    public sealed class EncodedGridExtension : AbstractHtmlExtension
    {
        protected override object ProcessDataInternal(in Scope scope)
        {
            var columns = (int) scope.GetParameter("columns");
            return "<grid cols=" + columns + ">" + (scope.ModelData?.ToString() ?? string.Empty) + "</grid>";
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessDataInternal(scope));
        }
    }

    /// <summary>The P8-J-E1 / D8 / WI5b fixture: a NO-parameter <c>[EncodeOutput]</c> custom extension shaped to
    /// provably hit <c>AllocateCustomExtension</c> — no <c>InitStart</c>/<c>CompleteInit</c> override (so
    /// <c>OverridesHook == false</c> and a bodiless call binds through the plain custom path, not the dynamic
    /// fallback), <c>[EncodeOutput]</c> without <c>[NotEncode]</c> (derived render type <c>Encode</c>, the
    /// previously-divergent value), markup-significant output (Encode vs Raw differ in bytes).</summary>
    [ExtensionName("encodedBare")]
    [EncodeOutput]
    public sealed class EncodedBareExtension : AbstractHtmlExtension
    {
        protected override object ProcessDataInternal(in Scope scope)
        {
            return "<b>&" + (scope.ModelData?.ToString() ?? string.Empty) + "</b>";
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessDataInternal(scope));
        }
    }

    /// <summary>Shared render shape for the malformed / re-declaration fixtures (behavior is irrelevant — only
    /// the declaration surface is under test).</summary>
    public abstract class EchoExtensionBase : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => scope.ModelData?.ToString() ?? string.Empty;

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>Malformed: two [Prop] on one class share a name → HED5007 (dynamic) / HED7017 (generator).</summary>
    [ExtensionName("malformedDup")]
    [Prop("a", typeof(int))]
    [Prop("a", typeof(string))]
    public sealed class MalformedDupExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: reserved parameter name → HED5015 / HED7017.</summary>
    [ExtensionName("malformedReserved")]
    [Prop("out", typeof(int))]
    public sealed class MalformedReservedExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: a null parameter name (not a usable name) → HED5015 / HED7017.</summary>
    [ExtensionName("malformedNullName")]
    [Prop(null, typeof(int))]
    public sealed class MalformedNullNameExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: default not convertible to the parameter type → HED5009 / HED7017.</summary>
    [ExtensionName("malformedDefault")]
    [Prop("a", typeof(int), Default = "x")]
    public sealed class MalformedDefaultExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: an unusable (open-generic) parameter type → HED5010 / HED7017.</summary>
    [ExtensionName("malformedType")]
    [Prop("a", typeof(System.Collections.Generic.List<>))]
    public sealed class MalformedTypeExtension : EchoExtensionBase
    {
    }

    /// <summary>Base layer for the reference-typed re-declaration pair: declares <c>item: string</c>.</summary>
    [Prop("item", typeof(string))]
    public abstract class StringItemBaseExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: re-declares the inherited <c>item</c> with a NON-assignable widening type
    /// (<c>object</c> is not assignable to <c>string</c>) → HED5008 / HED7017.</summary>
    [ExtensionName("wideningItem")]
    [Prop("item", typeof(object))]
    public sealed class WideningItemExtension : StringItemBaseExtension
    {
    }

    /// <summary>Base layer for the narrowing companion: declares <c>n: object</c> (optional).</summary>
    [Prop("n", typeof(object), Optional = true)]
    public abstract class ObjectItemBaseExtension : EchoExtensionBase
    {
    }

    /// <summary>Clean: re-declares the inherited <c>n</c> with an ASSIGNABLE narrowing type (string → object) —
    /// compiles clean and re-defaults the base slot (parity with definition-prop re-declaration).</summary>
    [ExtensionName("narrowItem")]
    [Prop("n", typeof(string), Default = "narrowed")]
    public sealed class NarrowItemExtension : ObjectItemBaseExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "n=" + (scope.GetParameter("n") ?? "null") + ":" + (scope.ModelData?.ToString() ?? string.Empty);
        }
    }

    // ---- The Nullable<T> re-declaration trio (the H2-c3 cross-tier oracle) ----

    /// <summary>Base: <c>c: IComparable</c>.</summary>
    [Prop("c", typeof(IComparable), Optional = true)]
    public abstract class IfaceBaseExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: re-declares <c>c</c> as <c>int?</c> — <c>IComparable</c> is NOT assignable-from
    /// <c>int?</c> at runtime (<c>Nullable&lt;T&gt;</c> implements no interfaces) → HED5008 / HED7017 (the
    /// (C)-exclusion a bare boxing predicate would silently accept).</summary>
    [ExtensionName("nullableIface")]
    [Prop("c", typeof(int?))]
    public sealed class NullableIfaceItemExtension : IfaceBaseExtension
    {
    }

    /// <summary>Base: <c>m: int</c>.</summary>
    [Prop("m", typeof(int), Default = 1)]
    public abstract class IntBaseExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: re-declares <c>m</c> as <c>int?</c> — <c>int</c> is not assignable-from <c>int?</c>
    /// → HED5008 / HED7017.</summary>
    [ExtensionName("nullableWiden")]
    [Prop("m", typeof(int?))]
    public sealed class NullableWidenItemExtension : IntBaseExtension
    {
    }

    /// <summary>Base: <c>n: int?</c>.</summary>
    [Prop("n", typeof(int?), Optional = true)]
    public abstract class NullableIntBaseExtension : EchoExtensionBase
    {
    }

    /// <summary>Clean: re-declares <c>n</c> as <c>int</c> — <c>int?</c> IS assignable-from <c>int</c> via
    /// reflection's underlying-value rule → clean compile, re-defaults (the case a bare
    /// identity|reference|boxing predicate would false-error). Rides the cross-tier differential.</summary>
    [ExtensionName("nullableNarrow")]
    [Prop("n", typeof(int), Default = 5)]
    public sealed class NullableNarrowItemExtension : NullableIntBaseExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "n=" + scope.GetParameter("n") + ":" + (scope.ModelData?.ToString() ?? string.Empty);
        }
    }
}
