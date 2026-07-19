namespace Heddle.Models
{
    /// <summary>
    /// An immutable counted range — the result of the <c>range(...)</c> built-in and the value
    /// <c>@for</c> iterates: <see cref="Start"/> (inclusive), <see cref="Last"/> (exclusive),
    /// stepped by <see cref="Step"/> (positive; default 1). <c>Start &gt;= Last</c> is an empty
    /// range. Value equality over the three fields; the positive-step contract is enforced by the
    /// <c>range</c> built-in (HED4001 / render throw), not by this type's constructors.
    /// </summary>
    public readonly struct Range : System.IEquatable<Range>
    {
        /// <summary>Creates a range iterating <paramref name="start"/> … <paramref name="last"/>−1 with step 1.</summary>
        public Range(int start, int last)
            : this(start, last, 1)
        {
        }

        /// <summary>Creates a range iterating <paramref name="start"/> … <paramref name="last"/>−1 by
        /// <paramref name="step"/>. Fields are assigned verbatim — no step validation here (the
        /// <c>range</c> built-in owns that policy).</summary>
        public Range(int start, int last, int step)
        {
            Start = start;
            Last = last;
            Step = step;
        }

        /// <summary>The inclusive lower bound (0 on a <c>default</c> instance).</summary>
        public int Start { get; }

        /// <summary>The exclusive upper bound — <c>Start &gt;= Last</c> renders empty.</summary>
        public int Last { get; }

        /// <summary>The increment (1 via the two-argument constructor).</summary>
        public int Step { get; }

        /// <summary>Value equality over <see cref="Start"/>/<see cref="Last"/>/<see cref="Step"/>.</summary>
        public bool Equals(Range other) => Start == other.Start && Last == other.Last && Step == other.Step;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Range other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            // Manual *397 fold: System.HashCode is unavailable on netstandard2.0, and the fold behaves
            // identically on every TFM without conditional compilation.
            unchecked
            {
                int h = Start;
                h = (h * 397) ^ Last;
                h = (h * 397) ^ Step;
                return h;
            }
        }

        /// <summary>Value equality.</summary>
        public static bool operator ==(Range left, Range right) => left.Equals(right);

        /// <summary>Value inequality.</summary>
        public static bool operator !=(Range left, Range right) => !left.Equals(right);

        /// <summary>Mirrors the call form the author would write — <c>range(2, 10, 2)</c>, or
        /// <c>range(2, 10)</c> when the step is 1 — invariant culture.</summary>
        public override string ToString()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return Step == 1
                ? string.Format(ci, "range({0}, {1})", Start, Last)
                : string.Format(ci, "range({0}, {1}, {2})", Start, Last, Step);
        }

#if !NETSTANDARD2_0
        /// <summary>Maps a from-start <see cref="System.Range"/> to a step-1 Heddle range.</summary>
        /// <exception cref="System.ArgumentException">Either endpoint is a from-end (<c>^</c>) index.</exception>
        public static Range FromSystemRange(System.Range range)
        {
            if (range.Start.IsFromEnd || range.End.IsFromEnd)
                throw new System.ArgumentException(
                    "A from-end index ('^') has no meaning for a counted Heddle range; supply from-start endpoints.",
                    nameof(range));
            return new Range(range.Start.Value, range.End.Value, 1);
        }
#endif
    }
}
