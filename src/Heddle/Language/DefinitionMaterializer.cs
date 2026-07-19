namespace Heddle.Language
{
    /// <summary>
    /// The phase 7 D4 (step 4) fill materialization — the single, parse-model-pure source of the layered
    /// <see cref="DefinitionItem"/> a matched region fill compiles to, consumed by BOTH backends (the dynamic
    /// tier's <c>RegionFillScope</c> and the generator's context-aware emission), so the final-form definition
    /// each emits is the same object graph. Deliberately not a blanket <see cref="DefinitionItem.OverrideWith"/>
    /// (which clobbers <c>ModelType</c> and drops the region flags — review F3).
    /// </summary>
    internal static class DefinitionMaterializer
    {
        /// <summary>
        /// Builds the effective definition for a matched public-region fill: <c>BaseDefinition</c> = the region
        /// default (per call site), body = the override body, <c>ModelType</c> = the override's narrowing
        /// <c>:: Type</c> when declared else the region default's, region flags preserved, and
        /// <c>Position</c> = the override declaration's span — so the pre-existing narrowing check
        /// (<c>WalkValidateDefinitionType</c>) and HED5019 land at the override (D7/F5).
        /// </summary>
        internal static DefinitionItem Materialize(RegionFillCandidate candidate, DefinitionItem regionDefault)
        {
            return new DefinitionItem(
                candidate.Name,
                candidate.Item.ParameterTemplate,
                regionDefault,
                modelType: candidate.NarrowingTypeName ?? regionDefault.ModelType)
            {
                Position = candidate.Position,
                IsRegion = true,
                IsPublicRegion = regionDefault.IsPublicRegion,
                Context = candidate.Item.Context
            };
        }
    }
}
