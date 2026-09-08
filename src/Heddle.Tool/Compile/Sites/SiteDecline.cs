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

        public override string ToString() =>
            Kind + " site" + (string.IsNullOrEmpty(Position) ? " ordinal " + Ordinal : " at " + Position);
    }
}
