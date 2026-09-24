namespace Heddle.Tool.Compile.Sites
{
    /// <summary>A site the printer declined: nothing is printed for it and the loader rebuilds it
    /// from data. Declines are listed in the template's HED7031 notice.</summary>
    internal sealed class SiteDecline
    {
        internal SiteDecline(string kind, int ordinal, string position, string why)
        {
            Kind = kind;
            Ordinal = ordinal;
            Position = position;
            Why = why;
        }

        internal string Kind { get; }

        internal int Ordinal { get; }

        /// <summary>Template offset text ("@12") or empty when the row carries no position.</summary>
        internal string Position { get; }

        internal string Why { get; }

        /// <summary>Whether the site was declined because generated code cannot name something it reads: a
        /// non-public type, or a member whose getter is not public.</summary>
        internal bool IsAccessibility =>
            Why != null && (Why.IndexOf("non-public type", System.StringComparison.Ordinal) >= 0 ||
                Why.IndexOf("is not a public instance property", System.StringComparison.Ordinal) >= 0);

        public override string ToString() =>
            Kind + " site" + (string.IsNullOrEmpty(Position) ? " ordinal " + Ordinal : " at " + Position) +
            (string.IsNullOrEmpty(Why) ? string.Empty : " (" + Why + ")");
    }
}
