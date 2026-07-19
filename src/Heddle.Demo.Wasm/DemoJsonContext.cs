using System.Text.Json.Serialization;

namespace Heddle.Demo.Wasm
{
    /// <summary>Source-generated System.Text.Json context (phase 9 D6): trim-safe, no reflection warm-up. camelCase
    /// property names, nulls omitted (so RenderResult emits exactly one of html/error).</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(InitResult))]
    [JsonSerializable(typeof(AnalyzeResult))]
    [JsonSerializable(typeof(CompleteResult))]
    [JsonSerializable(typeof(HoverResultDto))]
    [JsonSerializable(typeof(RenderResult))]
    internal partial class DemoJsonContext : JsonSerializerContext
    {
    }
}
