using System.Collections.Generic;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The model the Razor parity twin renders from (D1; ledger entry E5).
    ///
    /// This exists because Razor runtime compilation emits the view into a *separate* assembly, so
    /// the view cannot reach <see cref="TwinContent"/>'s <c>internal</c> members directly the way
    /// the in-assembly twins do. Everything the twin needs is therefore projected onto this public
    /// model — from <see cref="TwinContent"/>, never transcribed — so the Razor twin reads the same
    /// fragments as every other twin and cannot silently drift.
    ///
    /// The predecessor of this twin did drift: it carried a hand-duplicated 56 KB copy of the area
    /// dictionary under <c>TestSuite/RazorExtensions/</c>, and its layout had already diverged from
    /// <c>layout.heddle</c> unnoticed (an empty <c>logo-holder</c> against Heddle's
    /// <c>&lt;a href="/"&gt;</c>) precisely because nothing compared the two. Sharing the fixtures
    /// makes that class of bug impossible rather than merely unlikely.
    /// </summary>
    public sealed class RazorTwinModel
    {
        /// <summary>Reusable-section defaults, keyed as in the other twins' templates.</summary>
        public IReadOnlyDictionary<string, string> Sections { get; }

        /// <summary>Fixed no-argument component outputs, keyed as in the other twins' templates.</summary>
        public IReadOnlyDictionary<string, string> Components { get; }

        /// <summary>The area fragments — <see cref="TestSuite.Extensions.AreaComponent.Areas"/> itself.</summary>
        public IReadOnlyDictionary<string, string> Areas { get; }

        /// <summary>The area names in the order <c>layout.heddle</c> issues them.</summary>
        public IReadOnlyList<string> AreaOrder { get; }

        private RazorTwinModel(
            IReadOnlyDictionary<string, string> sections,
            IReadOnlyDictionary<string, string> components,
            IReadOnlyDictionary<string, string> areas,
            IReadOnlyList<string> areaOrder)
        {
            Sections = sections;
            Components = components;
            Areas = areas;
            AreaOrder = areaOrder;
        }

        /// <summary>Builds the model from the shared twin fixtures.</summary>
        public static RazorTwinModel Create() => new RazorTwinModel(
            TwinContent.Sections(),
            TwinContent.Components(),
            TwinContent.Areas,
            TwinContent.AreaOrder);
    }
}
