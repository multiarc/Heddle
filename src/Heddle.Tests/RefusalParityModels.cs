using System.Collections.Generic;

namespace Heddle.Tests
{
    /// <summary>One element of <see cref="RefusalParityRoot.Rows"/>. Carries a <c>tone</c> member that
    /// deliberately shadows the <c>tone</c> prop of the <c>panel</c> definition in
    /// <c>refusal-prop-read.heddle</c>: a refusal fragment that loses the enclosing definition's prop layout
    /// binds the name to this member instead of the prop, which is a byte divergence the parity sweep sees
    /// rather than a compile error it could also have reached by luck.</summary>
    public class RefusalParityRow
    {
        public string Label { get; set; }

        public string tone { get; set; }
    }

    /// <summary>Root model for the refusal-fragment parity fixtures: a root-only member for <c>::</c> reads
    /// and a populated element list, so the bodies the refused sites sit in actually execute.</summary>
    public class RefusalParityRoot
    {
        public RefusalParityRoot()
        {
            Banner = "ROOT";
            Rows = new List<RefusalParityRow>
            {
                new RefusalParityRow { Label = "one", tone = "member-one" },
                new RefusalParityRow { Label = "two", tone = "member-two" }
            };
        }

        public string Banner { get; set; }

        public IEnumerable<RefusalParityRow> Rows { get; set; }
    }
}
