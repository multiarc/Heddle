using System.Collections.Generic;
using System.Text;
using Heddle.Data;
using Heddle.Precompiled.CompiledForm;

namespace Heddle.Tool.Compile.Sites
{
    /// <summary>Prints one embedded C# site (P3-R5): the same static method the engine's
    /// <c>CSharpClassTemplate.tcs</c> emits — <c>ProcessData_S&lt;n&gt;(model, chained, root)</c>
    /// with <c>dynamic</c> where the engine used it — inside <c>namespace Heddle.Runtime</c> so
    /// name lookup is identical to the engine's generated assembly, preceded by the site's
    /// <c>@using</c> namespaces. Arithmetic wraps in <c>unchecked</c> exactly as the engine's
    /// template does, because the consumer's checked/unchecked setting is not the engine's. The
    /// table wrapper boxes the result, mirroring the engine's <c>Convert(call, object)</c>.</summary>
    internal static class CSharpSitePrinter
    {
        internal static bool TryPrint(FormCSharp site, string className, string methodName,
            out string classCode, out string wrapper, out string why)
        {
            classCode = null;
            wrapper = null;
            why = null;
            if (site == null)
            {
                why = "no C# record";
                return false;
            }

            if (string.IsNullOrEmpty(site.Source))
            {
                why = "empty C# source";
                return false;
            }

            string modelSpelling;
            string modelWhy;
            if (!TypeNamePrinter.TrySpell(ScopeType(site.ModelType), out modelSpelling, out modelWhy))
            {
                why = "model scope type: " + modelWhy;
                return false;
            }

            string chainedSpelling;
            string chainedWhy;
            if (!TypeNamePrinter.TrySpell(ScopeType(site.ChainedType), out chainedSpelling,
                out chainedWhy))
            {
                why = "chained scope type: " + chainedWhy;
                return false;
            }

            string rootSpelling;
            string rootWhy;
            if (!TypeNamePrinter.TrySpell(ScopeType(site.RootType), out rootSpelling, out rootWhy))
            {
                why = "root scope type: " + rootWhy;
                return false;
            }

            // The wrapper casts through the runtime Type (object for a dynamic scope), mirroring
            // the engine's Convert(param, scopeType.Type): the method's dynamic parameter then
            // binds dynamically, exactly as the engine's compiled method does.
            string modelCast;
            string chainedCast;
            string rootCast;
            string castWhy;
            if (!TryRuntimeCast(site.ModelType, out modelCast, out castWhy) ||
                !TryRuntimeCast(site.ChainedType, out chainedCast, out castWhy) ||
                !TryRuntimeCast(site.RootType, out rootCast, out castWhy))
            {
                why = "scope type: " + castWhy;
                return false;
            }

            var sb = new StringBuilder();
            sb.Append("    internal static class ").Append(className).Append('\n');
            sb.Append("    {\n");
            sb.Append("        public static object ").Append(methodName).Append("(")
                .Append(modelSpelling).Append(" model, ").Append(chainedSpelling).Append(" chained, ")
                .Append(rootSpelling).Append(" root)\n");
            sb.Append("        {\n");
            sb.Append("            return unchecked(").Append(site.Source).Append(");\n");
            sb.Append("        }\n");
            sb.Append("    }\n");
            classCode = sb.ToString();
            // Fully qualified: the wrapper lives in the generated namespace, the class in
            // Heddle.Runtime, and neither is in the other's lookup scope.
            wrapper = "(model, chained, root) => global::Heddle.Runtime." + className + "." +
                methodName + "(" + modelCast + "model, " + chainedCast + "chained, " + rootCast +
                "root)";
            return true;
        }

        private static ExType ScopeType(ExType type) => type;

        private static bool TryRuntimeCast(ExType type, out string cast, out string why)
        {
            cast = null;
            why = null;
            var runtime = type != null ? type.Type : typeof(object);
            if (runtime == null)
                runtime = typeof(object);
            string spelling;
            if (!TypeNamePrinter.TrySpell(runtime, out spelling, out why))
                return false;
            cast = "(" + spelling + ")";
            return true;
        }

        internal static void AppendUsings(StringBuilder sb, IEnumerable<string> usings)
        {
            // The engine's class template always carries these two alongside the site's @using
            // namespaces; the printed class mirrors it so the same source compiles.
            sb.Append("using System.Runtime.CompilerServices;\n");
            sb.Append("using System.Reflection;\n");
            if (usings == null)
                return;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            seen.Add("System.Runtime.CompilerServices");
            seen.Add("System.Reflection");
            foreach (var ns in usings)
            {
                if (string.IsNullOrWhiteSpace(ns) || !seen.Add(ns))
                    continue;
                sb.Append("using ").Append(ns).Append(";\n");
            }
        }
    }
}
