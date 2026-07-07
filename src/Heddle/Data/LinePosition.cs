namespace Heddle.Data
{
    /// <summary>A resolved line/offset position for a diagnostic. Kept in its own file (phase 7 D4) so it joins the
    /// shared front-end closure without dragging in the runtime-shaped <see cref="HeddleCompileResult"/>.</summary>
    public class LinePosition
    {
        public int Offset { get; set; }
        public int Line { get; set; }
        public int LineLength { get; set; }

        public override string ToString()
        {
            return $"{Line},{Offset}:{LineLength}";
        }
    }
}
