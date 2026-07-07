using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Tool
{
    /// <summary>
    /// The reusable render core behind the <c>heddle render</c> command (WI12): parses a JSON model into an
    /// <see cref="ExpandoObject"/> tree and renders a template through the full dynamic engine — the T4-successor
    /// codegen path (a build <c>Exec</c> step turns data into source without a runtime Heddle dependency in the
    /// output assembly). Kept engine-facing and side-effect-free so it is unit-testable independently of arg parsing
    /// and file IO.
    /// </summary>
    public static class HeddleRenderer
    {
        /// <summary>Renders <paramref name="templateText"/> against the model parsed from <paramref name="modelJson"/>
        /// (may be null/empty for a model-less template). <paramref name="rootPath"/> is the resolver root for
        /// <c>@partial</c>/<c>@&lt;&lt;</c> imports.</summary>
        public static string Render(string templateText, string modelJson, string rootPath = null)
        {
            if (templateText == null)
                throw new ArgumentNullException(nameof(templateText));

            var options = new TemplateOptions
            {
                RootPath = string.IsNullOrEmpty(rootPath) ? string.Empty : rootPath
            };

            var template = new HeddleTemplate(templateText, new CompileContext(options));
            if (!template.CompileResult.Success)
                throw new InvalidOperationException("Template compilation failed: " + template.CompileResult);

            var model = string.IsNullOrWhiteSpace(modelJson) ? null : ParseModel(modelJson);
            return template.Generate(model);
        }

        /// <summary>Parses a JSON document into a dynamic model the engine renders: objects become
        /// <see cref="ExpandoObject"/> (member access), arrays become <see cref="List{Object}"/> (for
        /// <c>@list</c>/<c>@for</c>), and scalars become their CLR primitives.</summary>
        internal static object ParseModel(string json)
        {
            using (var doc = JsonDocument.Parse(json))
                return Convert(doc.RootElement);
        }

        private static object Convert(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    IDictionary<string, object> obj = new ExpandoObject();
                    foreach (var prop in element.EnumerateObject())
                        obj[prop.Name] = Convert(prop.Value);
                    return obj;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(Convert(item));
                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l))
                        return l;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                default:
                    return null;
            }
        }
    }
}
