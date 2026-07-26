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
    }

    // Definition-invocation fixtures (phase 7 keystone).
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

    // Recursion fixture: a linked list the definition walks by calling itself.
    public sealed class TreeNode
    {
        public string Label { get; set; }
        public TreeNode Next { get; set; }
    }

    // Props/slots fixture (generated-code.md example 5).
    public sealed class Article
    {
        public string Title { get; set; }
        public string Summary { get; set; }
    }

    // Slot fixtures (phase 7 slots): a definition projects caller content through @out(value).
    public sealed class Menu
    {
        public System.Collections.Generic.List<MenuOption> Options { get; set; }
    }

    public sealed class MenuOption
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Phase 4 (generator plan) — the operator-guard differential corpus. One model carrying an operand of every
    // category the shared classification table distinguishes, so each of the seven documented deviations from C#
    // gets a named template rather than being covered "by not happening to appear in the corpus".
    // ---------------------------------------------------------------------------------------------------------
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

    /// <summary>A value type carrying both a user-defined <b>operator</b> (which the native tier honors) and
    /// user-defined <b>implicit conversions</b> (which it deliberately does not consult) — deviation 6.</summary>
    public readonly struct Money
    {
        public Money(decimal amount) => Amount = amount;

        public decimal Amount { get; }

        public static implicit operator Money(decimal value) => new Money(value);

        public static implicit operator decimal(Money value) => value.Amount;

        public static Money operator +(Money left, Money right) => new Money(left.Amount + right.Amount);

        public override string ToString() => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Phase-4 audit (2026-07-26) — the string-<c>+</c> row's divergence class made concrete. An implicit
    /// conversion <b>to string</b> whose result differs from <see cref="ToString"/>: C#'s <c>+</c> prefers the
    /// converted <c>string</c> overload (a better target than <c>object</c>), while the native tier's
    /// <c>EmitStringConcat</c> always goes through <c>string.Concat(object, object)</c> and therefore calls
    /// <c>ToString</c>. Deviation 6 — user-defined implicit conversions are not consulted — with visibly different
    /// rendered bytes on the two routes.</summary>
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
        public Money Total { get; set; }
        public bool? Approved { get; set; }

        /// <summary>Phase-4 audit (2026-07-26): a <c>long</c> shift count and a lifted integral, the two operand
        /// shapes the shift row degrades for. C# has no <c>&lt;&lt;(int, long)</c> operator at all, so emitting a
        /// wide count verbatim is CS0019 in the <i>consumer's</i> build, while the runtime narrows any integral count
        /// to <c>int</c> and renders — the same asymmetry deviation 1 has, on a row no fixture reached.</summary>
        public long Big { get; set; }

        public int? Maybe { get; set; }

        public Label Tag { get; set; }

        public Manufacturer Maker { get; set; }
        public Address Where { get; set; }

        /// <summary>Visible to the typed member tier (an internal getter passes the runtime filter) but invisible to
        /// the dynamic tier's binder, which binds in <c>Heddle</c>'s context — the OQ3 asymmetry.</summary>
        internal string Secret { get; set; }
    }

    /// <summary>Q8.1 / HED7025 fixture: <see cref="Payload"/> is <c>object</c>-typed, which the operand estimator
    /// classifies as <c>Unknown</c> on purpose (an object-typed operand carries no usable static facts). The path
    /// still *writes*, so the overload binder is reached with an argument it cannot describe — the side condition's
    /// case, which must stay a silent degrade because the generator has proved nothing about the runtime.</summary>
    public sealed class OverloadPayload
    {
        public object Payload { get; set; }

        public int Count { get; set; }
    }

    // Phase 8 (post-2.0) extension-parameter fixture model.
    public sealed class GridModel
    {
        public string Name { get; set; }
        public int Cols { get; set; }
    }

    // Phase 7 (post-2.0) named-content-region fixtures.
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
}
