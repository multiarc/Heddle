namespace Heddle.Language
{
    /// <summary>
    /// Single source of the layered <see cref="DefinitionItem"/> for matched region fills, consumed by both backends.
    /// Uses composition instead of <see cref="DefinitionItem.OverrideWith"/> to preserve ModelType and region flags.
    /// </summary>
    internal static class DefinitionMaterializer
    {
        /// <summary>
        /// Builds the effective definition for a matched public-region fill: BaseDefinition from region default,
        /// body from override, ModelType from override's narrowing <c>:: Type</c> or region default, region flags
        /// preserved, Position from override span. Ensures HED5019 narrowing validation lands at the override.
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
