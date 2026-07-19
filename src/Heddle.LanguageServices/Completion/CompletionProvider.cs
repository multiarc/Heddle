using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.LanguageServices.Completion
{
    /// <summary>
    /// Builds completion items as a pure projection of the analysis (phase 6 D12/D13): typed members via the
    /// scope map + the member-tier filter, definitions/extensions/functions from the live registries, props from
    /// the definition declarations. Never guesses when types are unknown (D12 rule 7).
    /// </summary>
    internal static class CompletionProvider
    {
        private static readonly string[] Keywords = { "this", "true", "false", "null" };

        internal static CompletionResult GetCompletions(DocumentAnalysis analysis, int offset,
            FunctionRegistry functions)
        {
            var context = ContextDetector.Detect(analysis, offset, out var passedProps);
            var registry = functions ?? FunctionRegistry.Default;

            switch (context.Kind)
            {
                case CompletionContextKind.CallableNames:
                    return new CompletionResult(CallableNames(analysis, registry));

                case CompletionContextKind.ExpressionPosition:
                {
                    var items = new List<CompletionItem>();
                    items.AddRange(Members(analysis.Scopes.GetModelTypesAt(offset)));
                    items.AddRange(FunctionItems(registry));
                    foreach (var keyword in Keywords)
                        items.Add(new CompletionItem(keyword, CompletionItemKind.Keyword, null, keyword));
                    return new CompletionResult(items);
                }

                case CompletionContextKind.RootMembers:
                {
                    var root = analysis.Scopes.RootType;
                    var types = root != null ? new List<ExType> { root } : new List<ExType>();
                    return new CompletionResult(Members(types));
                }

                case CompletionContextKind.MemberOfPrefix:
                {
                    var baseType = context.RootReference
                        ? analysis.Scopes.RootType
                        : FirstOrNull(analysis.Scopes.GetModelTypesAt(offset));
                    var resolved = ResolvePrefix(baseType, context.Prefix);
                    if (resolved == null || resolved.IsDynamic)
                        return CompletionResult.Empty;
                    return new CompletionResult(Members(new List<ExType> { resolved }));
                }

                case CompletionContextKind.NamedArgument:
                {
                    var definition = analysis.Definitions.FirstOrDefault(d => d.Name == context.CallName);
                    if (definition == null)
                        return CompletionResult.Empty;
                    var items = new List<CompletionItem>();
                    foreach (var prop in definition.Props)
                    {
                        if (passedProps.Contains(prop.Name))
                            continue;
                        var detail = prop.IsRequired
                            ? $"{prop.TypeName} (required)"
                            : $"{prop.TypeName} = {FormatDefault(prop.DefaultValue)}";
                        items.Add(new CompletionItem(prop.Name, CompletionItemKind.Prop, detail, prop.Name + ": "));
                    }

                    return new CompletionResult(items);
                }

                case CompletionContextKind.RegionOverride:
                {
                    // Phase 7 (WI5): offer the callee's PUBLIC region names at a call-body '<' override position,
                    // inserting the '<name:name>' fill form's name pair.
                    var callee = analysis.Definitions.FirstOrDefault(d => d.Name == context.CallName);
                    if (callee == null)
                        return CompletionResult.Empty;
                    var regionItems = new List<CompletionItem>();
                    foreach (var region in callee.Regions)
                    {
                        if (!region.IsPublic)
                            continue;
                        var detail = string.IsNullOrEmpty(region.TypeName) || region.TypeName == "object"
                            ? "region"
                            : $"region :: {region.TypeName}";
                        regionItems.Add(new CompletionItem(region.Name, CompletionItemKind.Definition, detail,
                            region.Name + ":" + region.Name));
                    }

                    return new CompletionResult(regionItems);
                }

                default:
                    return CompletionResult.Empty;
            }
        }

        private static IReadOnlyList<CompletionItem> CallableNames(DocumentAnalysis analysis, FunctionRegistry registry)
        {
            var items = new List<CompletionItem>();
            foreach (var definition in analysis.Definitions)
            {
                var detail = HoverProvider.DefinitionSignature(definition);
                items.Add(new CompletionItem(definition.Name, CompletionItemKind.Definition, detail, definition.Name));
            }

            foreach (var name in TemplateFactory.RegisteredNames().Distinct().OrderBy(n => n, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(name))
                    continue;
                items.Add(new CompletionItem(name, CompletionItemKind.Extension, "extension", name));
            }

            items.AddRange(FunctionItems(registry));
            return items;
        }

        private static IEnumerable<CompletionItem> FunctionItems(FunctionRegistry registry)
        {
            foreach (var group in registry.EnumerateOverloads().GroupBy(o => o.Name).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var overloads = group.ToList();
                var signature = FormatSignature(overloads[0]);
                var detail = overloads.Count > 1 ? $"{signature} + {overloads.Count - 1} overloads" : signature;
                yield return new CompletionItem(group.Key, CompletionItemKind.Function, detail, group.Key);
            }
        }

        private static IReadOnlyList<CompletionItem> Members(IReadOnlyList<ExType> types)
        {
            if (types == null || types.Count == 0)
                return Array.Empty<CompletionItem>();
            if (types.Any(t => t == null || t.IsDynamic))
                return Array.Empty<CompletionItem>();

            // Name-based intersection across the recorded call-site types (D13).
            Dictionary<string, List<PropertyInfo>> byName = null;
            foreach (var type in types)
            {
                var here = MemberPathResolver.GetVisibleProperties(type.Type)
                    .GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First());
                if (byName == null)
                {
                    byName = here.ToDictionary(kv => kv.Key, kv => new List<PropertyInfo> { kv.Value });
                }
                else
                {
                    foreach (var key in byName.Keys.ToList())
                    {
                        if (here.TryGetValue(key, out var prop))
                            byName[key].Add(prop);
                        else
                            byName.Remove(key); // missing at this site — excluded
                    }
                }
            }

            var items = new List<CompletionItem>();
            foreach (var pair in byName.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var distinctTypes = pair.Value.Select(p => new ExType(p.PropertyType).ToString()).Distinct().ToList();
                var detail = distinctTypes.Count == 1 ? distinctTypes[0] : "varies by call site";
                items.Add(new CompletionItem(pair.Key, CompletionItemKind.Property, detail, pair.Key));
            }

            return items;
        }

        private static ExType ResolvePrefix(ExType baseType, string[] segments)
        {
            if (baseType == null || segments == null || segments.Length == 0)
                return baseType;
            var resolution = MemberPathResolver.TryResolve(baseType, segments);
            if (resolution.Kind == MemberPathResolutionKind.Resolved)
                return resolution.ResultType;
            return null;
        }

        private static ExType FirstOrNull(IReadOnlyList<ExType> types)
        {
            return types != null && types.Count > 0 ? types[0] : null;
        }

        internal static string FormatSignature((string Name, MethodInfo Method, Type[] ParameterTypes, Type ReturnType) o)
        {
            var ret = Friendly(o.ReturnType);
            var pars = string.Join(", ", o.ParameterTypes.Select(Friendly));
            return $"{ret} {o.Name}({pars})";
        }

        internal static string Friendly(Type type)
        {
            if (type == null) return "void";
            if (type == typeof(void)) return "void";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(object)) return "object";
            if (type == typeof(object[])) return "object[]";
            return new ExType(type).ToString();
        }

        private static string FormatDefault(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s + "\"";
            if (value is bool b) return b ? "true" : "false";
            return value.ToString();
        }
    }
}
