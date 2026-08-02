namespace Heddle.Generator.IntegrationTests.Fixtures
{
    public sealed class Manufacturer
    {
        public string Name { get; set; }
        public Address Address { get; set; }
    }

    public sealed class Address
    {
        public string City { get; set; }
    }

    /// <summary>An unsigned member beside a signed one. Which of the two an arithmetic expression meets decides
    /// whether a constant the two tiers hold in different types can still be written through: promoting a
    /// <c>uint</c> against <c>int</c> reaches <c>long</c> on both tiers, and promoting it against another
    /// <c>uint</c> does not.</summary>
    public sealed class UnsignedMemberModel
    {
        public uint Ticks { get; set; }
        public int Count { get; set; }
        public string Label { get; set; }
    }

    /// <summary>A reference hop onto a nullable value, then a member of that value. The engine substitutes a default
    /// at the hop that failed and keeps walking, so <c>Inner.Maybe.HasValue</c> over a null <c>Inner</c> reads
    /// <c>HasValue</c> off <c>default(int?)</c>.</summary>
    public sealed class NullableHopModel
    {
        public NullableHolder Inner { get; set; }
    }

    public sealed class NullableHolder
    {
        public int? Maybe { get; set; }

        public System.DateTime? When { get; set; }
    }

    public sealed class Product
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Manufacturer Manufacturer { get; set; }
    }

    public sealed class Cart
    {
        public int Count { get; set; }
        public bool IsArchived { get; set; }
        public bool IsFeatured { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Name { get; set; }
        public Nested Nested { get; set; }
    }

    public sealed class Nested
    {
        public int Amount { get; set; }
    }

    public sealed class Catalog
    {
        public string Title { get; set; }
        public System.Collections.Generic.List<Product> Products { get; set; }

        /// <summary>An array, which reaches <c>IEnumerable</c> through its base type rather than its own interface
        /// list — the shape a naive enumerability check misses.</summary>
        public string[] Tags { get; set; }
    }

    public sealed class GreetingModel
    {
        public UserPayload Payload { get; set; }
    }

    public sealed class UserPayload
    {
        public UserInfo User { get; set; }
    }

    public sealed class UserInfo
    {
        public string Name { get; set; }
    }

    public sealed class TreeNode
    {
        public string Label { get; set; }
        public TreeNode Next { get; set; }
    }

    /// <summary>
    /// A link whose getters report being read. Both tiers walk the same path, so both must charge the model the
    /// same number of getter calls — a template that reads a logging, lazy, or query-backed property must not
    /// behave differently depending on which backend rendered it.
    /// </summary>
    public sealed class CountingChain
    {
        public static int Reads;

        private CountingChain _next;
        private int _value;

        public CountingChain Next
        {
            get
            {
                Reads++;
                return _next;
            }
            set => _next = value;
        }

        public int Value
        {
            get
            {
                Reads++;
                return _value;
            }
            set => _value = value;
        }

        public static CountingChain Of(int hops)
        {
            var head = new CountingChain { _value = 42 };
            for (int i = 0; i < hops; i++)
                head = new CountingChain { _next = head, _value = 42 };
            return head;
        }
    }

    public sealed class Article
    {
        public string Title { get; set; }
        public string Summary { get; set; }
    }

    public sealed class Menu
    {
        public System.Collections.Generic.List<MenuOption> Options { get; set; }
    }

    public sealed class MenuOption
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    /// <summary>A collection whose element type carries no members at all. <c>object</c> is a static type and not
    /// an absence of one, so the collection is enumerable and the body over it renders — which is what tells the
    /// enumerability rule apart from a refusal of anything the emitter cannot say much about.</summary>
    public sealed class ObjItems
    {
        public System.Collections.Generic.List<object> Items { get; set; }
    }

    public enum OrderStatus
    {
        Draft = 0,
        Open = 1,
        Closed = 2
    }

    [System.Flags]
    public enum OrderFlags
    {
        None = 0,
        Rush = 1,
        Gift = 2
    }

    /// <summary>Demonstrates divergence between native and C# semantics: user-defined operators are honored, but implicit conversions are not.</summary>
    public readonly struct Money
    {
        public Money(decimal amount) => Amount = amount;

        public decimal Amount { get; }

        public static implicit operator Money(decimal value) => new Money(value);

        public static implicit operator decimal(Money value) => value.Amount;

        public static Money operator +(Money left, Money right) => new Money(left.Amount + right.Amount);

        public override string ToString() => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public class Vehicle
    {
        public override string ToString() => "vehicle";
    }

    public sealed class Truck : Vehicle
    {
        public override string ToString() => "truck";
    }

    /// <summary>Implicit conversion to string differs from <c>ToString()</c>; C#'s <c>+</c> prefers the conversion, while native rendering calls <c>ToString</c>.</summary>
    public readonly struct Label
    {
        public Label(string text) => Text = text;

        public string Text { get; }

        public static implicit operator string(Label value) => "converted:" + value.Text;

        public override string ToString() => "tostring:" + Text;
    }

    public sealed class Order
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public OrderStatus Status { get; set; }
        public OrderFlags Flags { get; set; }

        /// <summary>The lifted-complement lane: <c>~FlagsMaybe</c> keeps the nullable enum type on both tiers.</summary>
        public OrderFlags? FlagsMaybe { get; set; }
        public Money Total { get; set; }
        public bool? Approved { get; set; }

        /// <summary>Tests shift operands with a long count and nullable int; C# has no <c>&lt;&lt;(int, long)</c> overload.</summary>
        public long Big { get; set; }

        public int? Maybe { get; set; }

        /// <summary>The nullable wide shift count — the lane the writer spells with a lifted <c>(int?)</c> cast.</summary>
        public long? BigMaybe { get; set; }

        /// <summary>The CS0173 ternary pair: verbatim C# refuses <c>int</c> against <c>uint</c> arms outright,
        /// while the engine promotes to <c>long</c> — which the writer now spells as a cast on both arms.</summary>
        public uint Unsigned { get; set; }

        public Label Tag { get; set; }

        public Manufacturer Maker { get; set; }
        public Address Where { get; set; }

        /// <summary>The widening reference pair: <c>Rig</c> implicitly reference-converts to <c>Ride</c>'s type.</summary>
        public Vehicle Ride { get; set; }
        public Truck Rig { get; set; }

        /// <summary>Visible to the typed member tier (an internal getter passes the runtime filter) but invisible to
        /// the dynamic tier's binder, which binds in <c>Heddle</c>'s context.</summary>
        internal string Secret { get; set; }
    }

    /// <summary>HED7025 fixture: <see cref="Payload"/> is <c>object</c>-typed, testing the overload binder's handling of operands with no static type information.</summary>
    public sealed class OverloadPayload
    {
        public object Payload { get; set; }

        public int Count { get; set; }
    }

    public sealed class GridModel
    {
        public string Name { get; set; }
        public int Cols { get; set; }
    }

    public class RegionArticle
    {
        public string Title { get; set; }
        public int Id { get; set; }
    }

    public sealed class RegionFeed
    {
        public System.Collections.Generic.List<RegionArticle> Articles { get; set; }
        public bool ShowHeading { get; set; }
    }

    /// <summary>Members whose types the CLR relates to one another and C# does not — arrays over element types the
    /// runtime reduces to a common representative, plus a lifted scalar. These are what a <c>:: T</c> call site is
    /// checked against, so the check's answer for each of them is observable as a whole template.</summary>
    public sealed class ArrayAcceptanceModel
    {
        public int[] Ints { get; set; } = { 1, 2 };
        public uint[] UInts { get; set; } = { 1, 2 };
        public ushort[] UShorts { get; set; } = { 1 };
        public System.DayOfWeek[] Days { get; set; } = { System.DayOfWeek.Monday };
        public System.IntPtr[] NInts { get; set; } = { (System.IntPtr) 1 };
        public System.UIntPtr[] NUInts { get; set; } = { (System.UIntPtr) 1 };
        public int? Lifted { get; set; } = 7;
    }

    /// <summary>A host whose element shadows one member with <c>new</c>, so a body compiled against the host and
    /// one compiled against the element print different text. That difference is what makes it observable which
    /// model a body shared by two call sites was actually compiled against.</summary>
    public class RegionShadowHost
    {
        public string Tag => "host";
        public System.Collections.Generic.List<RegionShadowElement> Items { get; set; }
    }

    public sealed class RegionShadowElement : RegionShadowHost
    {
        public new string Tag => "element";
    }

    /// <summary>An element that is not the host's own type at all: a body compiled against one cannot take the
    /// other, so a shared body makes the engine's cast fail rather than print the other text.</summary>
    public sealed class RegionUnrelatedHost
    {
        public string Tag { get; set; }
        public System.Collections.Generic.List<RegionUnrelatedElement> Items { get; set; }
    }

    public sealed class RegionUnrelatedElement
    {
        public string Tag { get; set; }
    }

    /// <summary>A model whose property type has no nullable form. Emitting `?.` against one does not compile,
    /// which the generated tier learned the hard way.</summary>
    public sealed class RefStructModel
    {
        public System.ReadOnlySpan<char> Buf => System.MemoryExtensions.AsSpan("hello");
    }

    /// <summary>Puts the ref-struct hop behind a reference hop, so the receiver it is spelled against is itself the
    /// result of a null-conditional.</summary>
    public sealed class NestedRefStructModel
    {
        public RefStructModel Inner { get; set; }
    }

    /// <summary>Counts reads of the reference hop a ref-struct hop sits behind, and answers differently each time.
    /// A receiver read twice is not merely slower — it is a different receiver.</summary>
    public sealed class CountingRefStructModel
    {
        public static int Reads;

        public RefStructModel Inner
        {
            get
            {
                Reads++;
                return Reads == 1 ? new RefStructModel() : null;
            }
        }
    }

    /// <summary>
    /// A model with an <c>internal</c> readable property. The engine reads it — the member tier accepts a
    /// public-or-internal getter declared on the receiver — but a compilation that only <i>references</i> this
    /// assembly cannot: Roslyn imports no member from metadata that the importing assembly could not name, so the
    /// generator sees a type with <c>Title</c> and nothing else.
    /// <para>This fixture only carries its point from a <b>referenced</b> assembly. The generator's compilation
    /// references the test assembly as metadata, which is what makes it one.</para>
    /// </summary>
    public sealed class InternalMemberModel
    {
        public string Title => "public";

        internal string Secret => "s3cret";

        /// <summary>The near miss: invisible to a referencing compilation for the same reason <see cref="Secret"/>
        /// is, and rejected by the engine too, so the tiers agree it is an error.</summary>
        private string Hidden => "no";
    }

    /// <summary>A model whose one member fails on demand — the ordinary kind of failure a definition body has to be
    /// able to survive, since the carrier that ran it is cached for the life of the process.</summary>
    public sealed class ExplodingModel
    {
        public bool Explode { get; set; }

        public string Value => Explode ? throw new System.InvalidOperationException("boom") : "ok";
    }

    /// <summary>An <c>internal</c> model type: nameable by the engine, which resolves model types by reflection over
    /// loaded assemblies, and un-nameable by generated code in any other assembly.</summary>
    internal sealed class InternalModel
    {
        public string Title => "hidden";
    }

    /// <summary>The member half and the type half pulled apart. <see cref="Title"/> is declared on a public base, so
    /// a referencing compilation may name it and the member check has nothing to object to — while the type it is
    /// read off still cannot appear in that compilation's source. Only a guard on the type itself catches this.</summary>
    public abstract class PublicTitleBase
    {
        public string Title => "inherited";
    }

    internal sealed class InternalDerivedModel : PublicTitleBase
    {
    }

    /// <summary>A model the consumer's compiler refuses to let anyone name. Reflection ignores <c>[Obsolete]</c>
    /// altogether, so the engine renders this exactly like any other model.</summary>
    [System.Obsolete("gone", true)]
    public sealed class ObsoleteErrorModel
    {
        public string Title => "obsolete";
    }

    /// <summary>The member half: the type may be named, one of its properties may not.</summary>
    public sealed class ObsoleteMemberModel
    {
        public string Title => "public";

        [System.Obsolete("gone", true)]
        public string Bad => "bad";
    }

    /// <summary>An error-obsolete <b>value</b> type, so the emitter's null-safe form has to spell it in a
    /// <c>default(T)</c> rather than merely reading through it.</summary>
    [System.Obsolete("gone", true)]
    public struct ObsoleteErrorValue
    {
        public int Amount { get; set; }
    }

    /// <summary>
    /// A perfectly nameable model with a perfectly nameable property whose <b>type</b> no consumer may name. The
    /// declaration is legal C# only because the property carries an <c>[Obsolete]</c> of its own — an obsolete
    /// context suppresses the diagnostic on the type it mentions — and that one is warning-level, which the emitter
    /// deliberately lets through. So neither the property nor the model raises anything, and the type the generated
    /// <c>default(T)</c> spells is a CS0619.
    /// </summary>
    public sealed class ObsoletePropertyTypeModel
    {
        [System.Obsolete("prefer nothing")]
        public ObsoleteErrorValue Balance { get; set; }
    }

    /// <summary>A perfectly nameable model with a perfectly nameable, perfectly visible property whose <b>type</b>
    /// no generated code could hold a value of in any assembly. Nothing about it is the template author's doing and
    /// nothing they can write in the template changes it, which is what separates it from the obsolete and internal
    /// fixtures above.</summary>
    public sealed unsafe class UnusablePropertyTypeModel
    {
        public int* Handle => null;
        public string Title => "ok";
    }

    /// <summary>A perfectly ordinary generic with an ordinary nested value type. It becomes unnameable only when a
    /// type argument the consumer may not spell is substituted into it, and the nested type carries no type argument
    /// of its own to show for it.</summary>
    public class NestingOuter<T>
    {
        public struct InnerValue
        {
            public int Amount { get; set; }
        }
    }

    /// <summary>
    /// A nameable member of a nameable model whose <b>type</b> is nested inside a generic constructed over a name no
    /// consumer may write. Spelling the inner type means spelling the outer one, type argument and all, so the
    /// <c>default(T)</c> the null-safe form emits is a CS0619 — off a property whose own declaration carries nothing
    /// but the warning-level attribute that made it legal to declare at all.
    /// </summary>
    public sealed class ObsoleteContainerArgumentModel
    {
        [System.Obsolete("an obsolete context is the only way to declare this")]
        public NestingOuter<ObsoleteErrorValue>.InnerValue Balance { get; set; }
    }

    /// <summary>A nameable type nested inside one that is not: C# reports the error on the <b>outer</b> name, which
    /// no spelling of the inner one can avoid.</summary>
    [System.Obsolete("gone", true)]
    public class ObsoleteOuterModel
    {
        public sealed class Inner
        {
            public string Title => "inner";
        }
    }

    /// <summary>The deprecation that must NOT degrade — warning-level <c>[Obsolete]</c> on the type and on a member.
    /// It is a note to the author, not a refusal, and a rule that could not tell the two apart would take every
    /// deprecated model in a codebase off the precompiled tier without saying so.</summary>
    [System.Obsolete("prefer something else")]
    public sealed class DeprecatedModel
    {
        [System.Obsolete("prefer Title")]
        public string Legacy => "legacy";

        public string Title => "deprecated";
    }

    /// <summary>
    /// One member per relation an accepted-type declaration can stand in to a value: identity, a nullable lift on
    /// either side, generic covariance through a class and through an interface, array covariance over a
    /// reference element and over the CLR's reduced value-type elements, and the members (<see cref="S"/>,
    /// <see cref="D"/>, <see cref="Longs"/>) that stand in none of them and must be refused.
    /// </summary>
    public sealed class AcceptanceModel
    {
        public int I { get; set; }
        public int? Maybe { get; set; }
        public long L { get; set; }
        public string S { get; set; }
        public decimal D { get; set; }
        public System.Collections.Generic.List<string> Strs { get; set; }
        public System.Collections.Generic.IList<string> IStrs { get; set; }
        public string[] StrArr { get; set; }
        public int[] Ints { get; set; }
        public uint[] UInts { get; set; }
        public System.DayOfWeek[] Days { get; set; }
        public long[] Longs { get; set; }
        public System.Collections.Generic.List<int> IntList { get; set; }
    }

    /// <summary>A static class as a model: nothing the engine minds, since it never declares a parameter of the
    /// model's type, and impossible for generated code, which does.</summary>
    public static class StaticModel
    {
        public static string Title => "static";
    }

    /// <summary>A collection reaching <c>IEnumerable&lt;T&gt;</c> at two different <c>T</c>. The runtime's
    /// reflection walk picks one of them and compiles the whole <c>@list</c> body against it; which one it picks is
    /// an order no build-time walk can reproduce, so the emitter has to know it cannot say rather than proceed with
    /// no element type at all.</summary>
    public sealed class AmbiguousEnumerable :
        System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IEnumerable<string>
    {
        System.Collections.Generic.IEnumerator<int>
            System.Collections.Generic.IEnumerable<int>.GetEnumerator()
        {
            yield return 1;
            yield return 2;
        }

        System.Collections.Generic.IEnumerator<string>
            System.Collections.Generic.IEnumerable<string>.GetEnumerator()
        {
            yield return "x";
            yield return "y";
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            yield return 1;
            yield return 2;
        }
    }

    public sealed class AmbiguousElementHolder
    {
        public AmbiguousEnumerable Multi { get; set; } = new AmbiguousEnumerable();
        public System.Collections.Generic.List<string> Single { get; set; } =
            new System.Collections.Generic.List<string> { "x", "y" };
        public System.Collections.Generic.List<int> Numbers { get; set; } =
            new System.Collections.Generic.List<int> { 1, 2 };
    }
}
