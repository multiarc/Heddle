using System.Collections.Generic;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    // Models for the Heddle-internal suites.
    //
    // These are TOP-LEVEL types, not nested ones, and that is load-bearing rather than stylistic.
    // A definition's `:: <type>` clause is lexed from the template text, and a nested type's
    // reflection name contains a '+' (`Outer+Inner`) that the clause's grammar does not accept — so
    // a suite whose models were nested inside it would fail to compile its own templates. Naming
    // them at namespace level keeps the spelling in the template identical to the spelling in C#.

    /// <summary>A card's model: the smallest thing a definition can be handed.</summary>
    public class PropsArticle
    {
        public string Title { get; set; }
        public string Summary { get; set; }
    }

    /// <summary>One option row, projected through a parameterized slot.</summary>
    public class PropsOption
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    /// <summary>The slot suite's host model.</summary>
    public class PropsMenu
    {
        public IEnumerable<PropsOption> Options { get; set; }
        public string Style { get; set; }
    }

    /// <summary>One list cell: a flag to branch on and a name to render.</summary>
    public class BranchCell
    {
        public bool F { get; set; }
        public string Name { get; set; }
    }

    /// <summary>A list long enough that per-iteration branch cost is visible.</summary>
    public class BranchListModel
    {
        public List<BranchCell> Items { get; set; }
    }

    /// <summary>A realistic document's flags: nested conditions, no branch ever published.</summary>
    public class BranchFlagModel
    {
        public bool IsFeatured { get; set; }
        public bool IsArchived { get; set; }
    }
}
