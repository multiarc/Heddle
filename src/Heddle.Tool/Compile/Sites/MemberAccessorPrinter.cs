using System;
using System.Reflection;
using System.Text;
using Heddle.Language.Members;
using Heddle.Precompiled.CompiledForm;

namespace Heddle.Tool.Compile.Sites
{
    /// <summary>Prints one member accessor site (P3-R3): the engine's start cast, one local per hop
    /// in the <see cref="HopForm"/> the engine chose for that hop, boxed exactly once when the last
    /// member is a value type. The hop form is recomputed with the engine's own
    /// <see cref="MemberHopRule.Form"/> over the recorded hop triple, so the rule exists once.</summary>
    internal static class MemberAccessorPrinter
    {
        internal static bool TryPrint(FormMemberRow row, string methodName, out string code,
            out string why)
        {
            code = null;
            why = null;
            if (row == null)
            {
                why = "no member record";
                return false;
            }

            if (row.StartType == null || row.StartType.IsDynamic)
            {
                why = "dynamic start type";
                return false;
            }

            string startSpelling;
            string startWhy;
            if (!TypeNamePrinter.TrySpell(row.StartType.Type, out startSpelling, out startWhy))
            {
                why = "start type: " + startWhy;
                return false;
            }

            if (row.Hops == null || row.Hops.Count == 0)
            {
                why = "empty member path";
                return false;
            }

            var hops = new HopPrint[row.Hops.Count];
            for (int i = 0; i < row.Hops.Count; i++)
            {
                var hop = row.Hops[i];
                if (hop.Declaring == null || hop.Member == null || string.IsNullOrEmpty(hop.Name))
                {
                    why = "hop " + i + " is dynamic";
                    return false;
                }

                string memberSpelling;
                string memberWhy;
                if (!TypeNamePrinter.TrySpell(hop.Member, out memberSpelling, out memberWhy))
                {
                    why = "hop " + i + " member type: " + memberWhy;
                    return false;
                }

                string declaringSpelling;
                string declaringWhy;
                if (!TypeNamePrinter.TrySpell(hop.Declaring, out declaringSpelling, out declaringWhy))
                {
                    why = "hop " + i + " declaring type: " + declaringWhy;
                    return false;
                }

                // Positive evidence only: when the bound property re-resolves, its spelling must be
                // the callable one (public instance getter, no [Obsolete(error: true)]). A hop the
                // lookup cannot re-resolve was still bound by the engine, so it stays printable.
                var property = TypeNamePrinter.ResolveHop(hop.Declaring, hop.Name);
                if (property != null)
                {
                    if (!TypeNamePrinter.IsCallableFromConsumer(property))
                    {
                        why = "hop " + i + " '" + hop.Name + "' is not a public instance property";
                        return false;
                    }

                    if (TypeNamePrinter.IsObsoleteError(property))
                    {
                        why = "hop " + i + " '" + hop.Name + "' is [Obsolete(error: true)]";
                        return false;
                    }
                }

                var form = MemberHopRule.Form(hop.Declaring.IsValueType,
                    hop.Member.IsValueType && Nullable.GetUnderlyingType(hop.Member) == null);
                hops[i] = new HopPrint
                {
                    Name = hop.Name,
                    MemberSpelling = memberSpelling,
                    MemberIsValue = hop.Member.IsValueType &&
                        Nullable.GetUnderlyingType(hop.Member) == null,
                    Form = form
                };
            }

            var sb = new StringBuilder();
            sb.Append("        private static object ").Append(methodName).Append("(object model)\n");
            sb.Append("        {\n");
            sb.Append("            var v0 = (").Append(startSpelling).Append(")model;\n");
            for (int i = 0; i < hops.Length; i++)
            {
                string receiver = "v" + i;
                string target = "v" + (i + 1);
                bool last = i == hops.Length - 1;
                var hop = hops[i];
                if (hop.Form == HopForm.Direct)
                {
                    if (last && hop.MemberIsValue)
                    {
                        sb.Append("            return (object)").Append(receiver).Append(".")
                            .Append(hop.Name).Append(";\n");
                    }
                    else if (last)
                    {
                        sb.Append("            return ").Append(receiver).Append(".").Append(hop.Name)
                            .Append(";\n");
                    }
                    else
                    {
                        sb.Append("            var ").Append(target).Append(" = ").Append(receiver)
                            .Append(".").Append(hop.Name).Append(";\n");
                    }
                }
                else
                {
                    string nullArm = hop.MemberIsValue
                        ? "default(" + hop.MemberSpelling + ")"
                        : "null";
                    if (last)
                    {
                        if (hop.MemberIsValue)
                        {
                            sb.Append("            var ").Append(target).Append(" = ").Append(receiver)
                                .Append(" == null ? ").Append(nullArm).Append(" : ").Append(receiver)
                                .Append(".").Append(hop.Name).Append(";\n");
                            sb.Append("            return (object)").Append(target).Append(";\n");
                        }
                        else
                        {
                            sb.Append("            return ").Append(receiver).Append(" == null ? null : (object)")
                                .Append(receiver).Append(".").Append(hop.Name).Append(";\n");
                        }
                    }
                    else
                    {
                        sb.Append("            var ").Append(target).Append(" = ").Append(receiver)
                            .Append(" == null ? ").Append(nullArm).Append(" : ").Append(receiver)
                            .Append(".").Append(hop.Name).Append(";\n");
                    }
                }
            }

            // A single Direct reference hop prints its own return; every other shape returns above.
            // The uniform local path (value hops) returns inside the loop; nothing falls through.
            sb.Append("        }\n");
            code = sb.ToString();
            return true;
        }

        private sealed class HopPrint
        {
            internal string Name;
            internal string MemberSpelling;
            internal bool MemberIsValue;
            internal HopForm Form;
        }
    }
}
