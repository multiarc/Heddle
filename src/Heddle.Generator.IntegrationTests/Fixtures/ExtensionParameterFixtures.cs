using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

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

    /// <summary><c>[EncodeOutput]</c> with <c>[Prop]</c>: carrier stays attribute-transparent so inner self-encodes.</summary>
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

    /// <summary><c>[EncodeOutput]</c> with no parameters: hits <c>AllocateCustomExtension</c> with render type <c>Encode</c>.</summary>
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

    /// <summary>Base: <c>e: Enum</c>.</summary>
    [Prop("e", typeof(Enum), Optional = true)]
    public abstract class EnumBaseExtension : EchoExtensionBase
    {
    }

    /// <summary>Malformed: re-declares <c>e</c> as <c>DayOfWeek?</c>. Roslyn classifies the boxing of the
    /// underlying enum, which does derive from <c>Enum</c>; the CLR relates <c>Nullable&lt;T&gt;</c> itself, whose
    /// base chain is <c>ValueType</c> and stops there → HED5008 / HED7017. The class-target twin of the interface
    /// exclusion above, which a correction phrased as "except an interface" leaves behind.</summary>
    [ExtensionName("nullableEnum")]
    [Prop("e", typeof(DayOfWeek?))]
    public sealed class NullableEnumItemExtension : EnumBaseExtension
    {
    }

    /// <summary>Base: <c>v: ValueType</c> — which is on <c>Nullable&lt;T&gt;</c>'s own base chain, where
    /// <c>Enum</c> is not.</summary>
    [Prop("v", typeof(ValueType), Optional = true)]
    public abstract class ValueTypeBaseExtension : EchoExtensionBase
    {
    }

    /// <summary>Clean: re-declares <c>v</c> as the same <c>DayOfWeek?</c> the row above declares, against the one
    /// target the CLR does relate it to. The pair says the rule is <c>Nullable&lt;T&gt;</c>'s own hierarchy and
    /// not "refuse every nullable re-declaration".</summary>
    [ExtensionName("nullableValueType")]
    [Prop("v", typeof(DayOfWeek?), Optional = true)]
    public sealed class NullableValueTypeItemExtension : ValueTypeBaseExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "v=" + (scope.GetParameter("v")?.ToString() ?? "none") + ":" +
                   (scope.ModelData?.ToString() ?? string.Empty);
        }
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

    /// <summary>A value-type default on an <c>object</c>-typed prop — the one arm of the default conversion that
    /// needs boxing, and the reason prop defaults ask the conversion table with boxing switched on while a slot
    /// value asks with it switched off.</summary>
    [ExtensionName("boxedDefault")]
    [Prop("n", typeof(object), Default = 5)]
    public sealed class BoxedDefaultExtension : EchoExtensionBase
    {
        public override object ProcessData(in Scope scope)
        {
            var value = scope.GetParameter("n");
            return "n=" + value + "/" + (value?.GetType().Name ?? "null") + ":" +
                   (scope.ModelData?.ToString() ?? string.Empty);
        }
    }

    /// <summary>Widening default <c>int</c> into <c>long?</c>: must produce boxed <see cref="long"/>, not <c>int</c>.</summary>
    [ExtensionName("nullableLiftDefault")]
    [Prop("n", typeof(long?), Default = 5)]
    public sealed class NullableLiftDefaultExtension : EchoExtensionBase
    {
        public override object ProcessData(in Scope scope)
        {
            var value = scope.GetParameter("n");
            return "n=" + value + "/" + (value?.GetType().Name ?? "null") + ":" +
                   (scope.ModelData?.ToString() ?? string.Empty);
        }
    }

    /// <summary>A prop type generated code may not spell: <c>internal</c> to this assembly, so a cast written
    /// against it in the consumer's own compilation would be CS0122. Nothing on the extension-parameter path ever
    /// spells a prop type — the prototype stores boxed values, the parameter-name field stores strings, and the
    /// fingerprint is a manifest string — so this is the fixture that says whether refusing such a layout prevents
    /// anything or only costs a working template.</summary>
    internal sealed class InternalBadge
    {
        public override string ToString() => "badge";
    }

    /// <summary>Declares one prop of an unnameable type and one of a perfectly ordinary one, so a test can tell
    /// "this prop" from "this extension".</summary>
    [ExtensionName("badged")]
    [Prop("badge", typeof(InternalBadge), Optional = true)]
    [Prop("size", typeof(int), Default = 2)]
    public sealed class BadgedExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            var badge = scope.GetParameter("badge");
            return "badge=" + (badge?.ToString() ?? "none") + "/size=" + scope.GetParameter("size") + ":" +
                   (scope.ModelData?.ToString() ?? string.Empty);
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>Renders the <c>day</c> parameter <b>with its runtime type</b>. A default stored under the wrong
    /// CLR type still prints the right digits for some values, so the type is the part worth asserting.</summary>
    public abstract class DayEchoExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            var value = scope.GetParameter("day");
            return "day=" + value + "/" + (value?.GetType().Name ?? "null");
        }

        public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
    }

    /// <summary>A prop of an enum type with a default of that same enum — the identity arm of the conversion.</summary>
    [ExtensionName("enumDefault")]
    [Prop("day", typeof(DayOfWeek), Default = DayOfWeek.Tuesday)]
    public sealed class EnumDefaultExtension : DayEchoExtension
    {
    }

    /// <summary>The same with the enum's zero member: the underlying primitive is then the default value of every
    /// integral type, which is exactly the value a dropped type hides behind.</summary>
    [ExtensionName("enumZeroDefault")]
    [Prop("day", typeof(DayOfWeek), Default = DayOfWeek.Sunday)]
    public sealed class EnumZeroDefaultExtension : DayEchoExtension
    {
    }

    /// <summary>An enum whose underlying type is not <c>int</c>, so a default written as an <c>int</c> literal
    /// would be a differently-sized box as well as a differently-named one.</summary>
    public enum Rung : byte
    {
        Low = 0,
        High = 7
    }

    [ExtensionName("byteEnumDefault")]
    [Prop("day", typeof(Rung), Default = Rung.High)]
    public sealed class ByteEnumDefaultExtension : DayEchoExtension
    {
    }

    /// <summary>The lifted form: a <c>Nullable&lt;enum&gt;</c> prop boxes the enum itself, not the nullable.</summary>
    [ExtensionName("nullableEnumDefault")]
    [Prop("day", typeof(DayOfWeek?), Default = DayOfWeek.Friday)]
    public sealed class NullableEnumDefaultExtension : DayEchoExtension
    {
    }

    /// <summary>The boxing arm: an enum default on an <c>object</c>-typed prop passes through unconverted, so what
    /// the prop holds is a boxed enum and every read that formats or types it can tell.</summary>
    [ExtensionName("objectEnumDefault")]
    [Prop("day", typeof(object), Default = DayOfWeek.Tuesday)]
    public sealed class ObjectEnumDefaultExtension : DayEchoExtension
    {
    }

    /// <summary>The contrast that proves the enum <em>type</em> is what the other fixtures are about, not the
    /// default machinery: an <c>int</c> default against the same enum prop is a declaration both tiers refuse.</summary>
    [ExtensionName("enumIntDefault")]
    [Prop("day", typeof(DayOfWeek), Default = 2)]
    public sealed class EnumIntDefaultExtension : DayEchoExtension
    {
    }

    /// <summary>An enum this assembly keeps to itself: naming it in generated code would be CS0122 in the
    /// consumer's build, so its default has no reproducible form.</summary>
    internal enum InternalRung
    {
        One = 1
    }

    [ExtensionName("internalEnumDefault")]
    [Prop("day", typeof(InternalRung), Default = InternalRung.One)]
    public sealed class InternalEnumDefaultExtension : DayEchoExtension
    {
    }

    /// <summary>The four integral types narrower than <c>int</c>, which C# gives no literal suffix.</summary>
    [ExtensionName("narrowDefaults")]
    [Prop("b", typeof(byte), Default = (byte) 5)]
    [Prop("sb", typeof(sbyte), Default = (sbyte) -5)]
    [Prop("s", typeof(short), Default = (short) -300)]
    [Prop("us", typeof(ushort), Default = (ushort) 400)]
    public sealed class NarrowDefaultsExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) =>
            One(scope, "b") + ";" + One(scope, "sb") + ";" + One(scope, "s") + ";" + One(scope, "us");

        private static string One(in Scope scope, string name)
        {
            var value = scope.GetParameter(name);
            return name + "=" + value + "/" + (value?.GetType().Name ?? "null");
        }

        public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
    }
}
