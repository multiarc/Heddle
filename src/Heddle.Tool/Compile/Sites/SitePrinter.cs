using System.Collections.Generic;
using System.Text;
using Heddle.Precompiled.CompiledForm;

namespace Heddle.Tool.Compile.Sites
{
    /// <summary>One printed site: the table entry the loader's <c>TryGetSite</c> serves.</summary>
    internal sealed class PrintedSite
    {
        internal int TemplateIndex;
        internal int Ordinal;
        internal string Kind;
        internal string DelegateType;
        internal string FieldName;
        internal string MethodName;
        internal string MethodCode;
    }

    /// <summary>One template's printer output: the site methods, the C# classes, and the declines
    /// the HED7031 notice lists.</summary>
    internal sealed class TemplateSites
    {
        internal readonly List<PrintedSite> Sites = new List<PrintedSite>();
        internal readonly List<SiteDecline> Declines = new List<SiteDecline>();
        internal readonly List<string> CSharpClasses = new List<string>();
        internal readonly List<string> CSharpUsings = new List<string>();
    }

    /// <summary>Prints the generated sites for one template (P3-R2): one typed static method per
    /// member accessor, native expression and embedded C# site, addressable by site id through the
    /// site table. Reads the build's in-memory form record (live types, bound trees) correlated by
    /// payload index against the merged artifact rows; a site the rules decline prints nothing and
    /// is recorded for HED7031, and the loader rebuilds it from data.</summary>
    internal static class SitePrinter
    {
        internal sealed class Input
        {
            internal FormRecord Record;
            internal string ContentHash;
            internal int TemplateIndex;
            internal int MemberBase;
            internal int ExpressionBase;
            internal int CSharpBase;
        }

        internal static TemplateSites Print(Input input, CompiledArtifact artifact)
        {
            var output = new TemplateSites();
            if (input == null || input.Record == null || artifact == null || artifact.Sites == null)
                return output;
            foreach (var row in artifact.Sites)
            {
                if (row == null || row.TemplateIndex != input.TemplateIndex)
                    continue;
                switch (row.Kind)
                {
                    case CompiledSiteKind.MemberAccessor:
                        PrintMember(input, row, output);
                        break;
                    case CompiledSiteKind.NativeExpression:
                        PrintNative(input, row, artifact, output);
                        break;
                    case CompiledSiteKind.EmbeddedCSharp:
                        PrintCSharp(input, row, output);
                        break;
                    default:
                        // Late-bound and refusal rows are data by decision (P3-R2 declines nothing
                        // here): the table carries no delegate and the loader rebuilds or, under
                        // strict load, throws the site's own kind.
                        break;
                }
            }

            return output;
        }

        private static void PrintMember(Input input, CompiledSiteRow row, TemplateSites output)
        {
            int local = row.PayloadRef - input.MemberBase;
            string position = null;
            FormMemberRow member = null;
            if (local >= 0 && local < input.Record.MemberRows.Count)
            {
                member = input.Record.MemberRows[local];
                int offset = input.Record.FindMemberPosition(local);
                if (offset >= 0)
                    position = "@" + offset;
            }

            string methodName = "M_" + input.TemplateIndex + "_" + row.SiteOrdinal;
            string code;
            string why;
            if (member == null)
            {
                output.Declines.Add(new SiteDecline("MemberAccessor", row.SiteOrdinal, position,
                    "member record out of range"));
                return;
            }

            if (!MemberAccessorPrinter.TryPrint(member, methodName, out code, out why))
            {
                output.Declines.Add(new SiteDecline("MemberAccessor", row.SiteOrdinal, position, why));
                return;
            }

            output.Sites.Add(new PrintedSite
            {
                TemplateIndex = input.TemplateIndex,
                Ordinal = row.SiteOrdinal,
                Kind = "MemberAccessor",
                DelegateType = "Func<object, object>",
                FieldName = "S_" + input.TemplateIndex + "_" + row.SiteOrdinal,
                MethodName = methodName,
                MethodCode = code
            });
        }

        private static void PrintNative(Input input, CompiledSiteRow row, CompiledArtifact artifact,
            TemplateSites output)
        {
            int local = row.PayloadRef - input.ExpressionBase;
            FormExprTree tree = null;
            if (local >= 0 && local < input.Record.ExpressionTrees.Count)
                tree = input.Record.ExpressionTrees[local];
            string position = NativePosition(artifact, row);
            if (tree == null)
            {
                output.Declines.Add(new SiteDecline("NativeExpression", row.SiteOrdinal, position,
                    "expression record out of range"));
                return;
            }

            if (tree.Deferred)
            {
                // Late-bound sites are never generated (P3-R2); the load registry binds them.
                output.Declines.Add(new SiteDecline("NativeExpression", row.SiteOrdinal, position,
                    "deferred call binds at load"));
                return;
            }

            string methodName = "M_" + input.TemplateIndex + "_" + row.SiteOrdinal;
            string code;
            string why;
            if (!NativeExpressionPrinter.TryPrint(tree.Bound, tree.UsesProps, methodName, out code,
                out why))
            {
                output.Declines.Add(new SiteDecline("NativeExpression", row.SiteOrdinal, position, why));
                return;
            }

            output.Sites.Add(new PrintedSite
            {
                TemplateIndex = input.TemplateIndex,
                Ordinal = row.SiteOrdinal,
                Kind = "NativeExpression",
                DelegateType = tree.UsesProps
                    ? "Func<object, object, object, object[], object>"
                    : "Func<object, object, object, object>",
                FieldName = "S_" + input.TemplateIndex + "_" + row.SiteOrdinal,
                MethodName = methodName,
                MethodCode = code
            });
        }

        private static string NativePosition(CompiledArtifact artifact, CompiledSiteRow row)
        {
            if (artifact.Expressions == null || row.PayloadRef < 0 ||
                row.PayloadRef >= artifact.Expressions.Count)
                return null;
            var tree = artifact.Expressions[row.PayloadRef];
            if (tree == null || tree.Root == null || tree.Root.Position == null)
                return null;
            return "@" + tree.Root.Position.Start + "+" + tree.Root.Position.Length;
        }

        private static void PrintCSharp(Input input, CompiledSiteRow row, TemplateSites output)
        {
            int local = row.PayloadRef - input.CSharpBase;
            FormCSharp site = null;
            if (local >= 0 && local < input.Record.CSharpSites.Count)
                site = input.Record.CSharpSites[local];
            string position = CSharpPosition(input, local);
            if (site == null)
            {
                output.Declines.Add(new SiteDecline("CSharp", row.SiteOrdinal, position,
                    "C# record out of range"));
                return;
            }

            string className = "CSE_T" + input.TemplateIndex + "S" + row.SiteOrdinal;
            string methodName = "ProcessData_S" + row.SiteOrdinal;
            string classCode;
            string wrapper;
            string why;
            if (!CSharpSitePrinter.TryPrint(site, className, methodName, out classCode, out wrapper,
                out why))
            {
                output.Declines.Add(new SiteDecline("CSharp", row.SiteOrdinal, position, why));
                return;
            }

            output.CSharpClasses.Add(classCode);
            foreach (var ns in site.Usings)
            {
                bool seen = false;
                foreach (var existing in output.CSharpUsings)
                {
                    if (existing == ns)
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                    output.CSharpUsings.Add(ns);
            }

            output.Sites.Add(new PrintedSite
            {
                TemplateIndex = input.TemplateIndex,
                Ordinal = row.SiteOrdinal,
                Kind = "CSharp",
                DelegateType = "Func<object, object, object, object>",
                FieldName = "S_" + input.TemplateIndex + "_" + row.SiteOrdinal,
                MethodName = null,
                MethodCode = wrapper
            });
        }

        private static string CSharpPosition(Input input, int local)
        {
            if (input.Record == null || local < 0 || local >= input.Record.CSharpSites.Count)
                return null;
            var site = input.Record.CSharpSites[local];
            if (site == null || site.Position.StartIndex < 0)
                return null;
            return "@" + site.Position.StartIndex + "+" + site.Position.Length;
        }

        /// <summary>The table dispatch: one nested arm per template, one case per printed site. A
        /// delegate the table does not serve (declined or absent) returns false and the loader
        /// rebuilds from data; a digest mismatch never reaches here (the loader fails fast).</summary>
        internal static void AppendTable(StringBuilder sb, string contentHash, int templateIndex,
            IReadOnlyList<PrintedSite> sites)
        {
            sb.Append("            if (templateIndex == ").Append(templateIndex)
                .Append(" && string.Equals(templateContentHash, \"").Append(contentHash)
                .Append("\", System.StringComparison.Ordinal))\n");
            sb.Append("            {\n");
            sb.Append("                switch (siteOrdinal)\n");
            sb.Append("                {\n");
            foreach (var site in sites)
            {
                sb.Append("                    case ").Append(site.Ordinal).Append(": site = ")
                    .Append(site.FieldName).Append("; return true;\n");
            }

            sb.Append("                    default: site = null; return false;\n");
            sb.Append("                }\n");
            sb.Append("            }\n");
        }
    }
}
