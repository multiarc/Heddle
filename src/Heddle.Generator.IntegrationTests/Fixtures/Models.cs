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
}
