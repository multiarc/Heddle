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
        public Money Total { get; set; }
        public bool? Approved { get; set; }

        /// <summary>Tests shift operands with a long count and nullable int; C# has no <c>&lt;&lt;(int, long)</c> overload.</summary>
        public long Big { get; set; }

        public int? Maybe { get; set; }

        public Label Tag { get; set; }

        public Manufacturer Maker { get; set; }
        public Address Where { get; set; }

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
}
