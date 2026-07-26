using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Published documentation may not name an engine member that is not public. A doc naming a removed or internal
    /// member is worse than a missing page: a reader writes the call, and only the compiler disagrees.
    /// <para><b>What is checked.</b> Backticked <c>Type.Member</c> mentions in <c>docs/*.md</c> whose <c>Type</c> is a
    /// simple name the public-API goldens declare. Those are the mentions prose actually uses — <c>TemplateOptions</c>,
    /// <c>Scope</c>, <c>PrecompiledTemplates</c> — and a member absent from that type's golden block is either not
    /// public or was removed. Fully qualified <c>Heddle.Ns.Type</c> mentions are checked the same way. Generic and array
    /// suffixes are stripped, because the goldens spell them differently from prose.</para>
    /// <para><b>Known limits, stated rather than hidden.</b> A bare type name with no member is not checked: too many
    /// PascalCase words in prose are enum members, MSBuild metadata, or ordinary English. Neither are MSBuild property
    /// and item-metadata names, which carry no namespace and are not CLR members — those stay review-only. And a name
    /// under a non-engine <c>Heddle.</c> prefix (assembly and package ids, the generated-code namespace, project names,
    /// the <c>Heddle.CSharpTierEnabled</c> switch) is not engine surface at all.</para>
    /// </summary>
    public class PublicApiDocMentionTests
    {
        [Fact]
        public void EveryDocumentedMemberIsInThePublicApiGolden()
        {
            var surface = Surface();
            var unresolved = new List<string>();
            var checkedCount = 0;

            foreach (var mention in Mentions())
            {
                var split = mention.Value.LastIndexOf('.');
                if (split <= 0 || split == mention.Value.Length - 1)
                    continue;
                var typeName = mention.Value.Substring(0, split);
                var member = mention.Value.Substring(split + 1);

                if (!surface.BySimpleName.TryGetValue(typeName.Split('.').Last(), out var candidates))
                    continue;
                if (typeName.Contains('.') && !candidates.Contains(typeName))
                    continue;

                checkedCount++;
                if (candidates.Any(full => surface.Members.Contains(full + "." + member)))
                    continue;

                unresolved.Add(mention.Value + " (" + mention.Key + ")");
            }

            Assert.True(checkedCount >= 50,
                "Only " + checkedCount + " documented Type.Member mentions were checkable — the backtick convention " +
                "or the golden shape changed, and this gate is no longer checking anything.");

            Assert.True(unresolved.Count == 0,
                "Published documents name engine members absent from the public-API golden: " +
                string.Join("; ", unresolved.Distinct(StringComparer.Ordinal).OrderBy(u => u, StringComparer.Ordinal)) +
                ". Either the member is not public, or the golden is stale — establish which before editing either.");
        }

        /// <summary>Drops generic and array suffixes, and any trailing punctuation prose leaves behind.</summary>
        private static string Normalize(string mention)
        {
            var name = mention;
            var generic = name.IndexOf('<');
            if (generic >= 0)
                name = name.Substring(0, generic);
            name = name.Replace("()", string.Empty).Replace("[]", string.Empty);
            return name.TrimEnd('.', ' ');
        }

        /// <summary>Backticked <c>Type.Member</c> (optionally namespace-qualified) mentions, keyed by document.</summary>
        private static List<KeyValuePair<string, string>> Mentions()
        {
            var mentions = new List<KeyValuePair<string, string>>();
            foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "docs"), "*.md"))
            {
                var text = File.ReadAllText(file);
                foreach (Match match in Regex.Matches(text,
                             @"`(?<name>(?:[A-Za-z_][A-Za-z0-9_]*\.)*[A-Z][A-Za-z0-9_]*(?:<[^`]*>)?\.[A-Za-z_][A-Za-z0-9_]*)(?:<[^`]*>)?(?:\(\))?`"))
                    mentions.Add(new KeyValuePair<string, string>(
                        Path.GetFileName(file), Normalize(match.Groups["name"].Value)));
            }

            return mentions;
        }

        /// <summary>The goldens' types indexed by simple name, plus every <c>Namespace.Type.Member</c> pair.</summary>
        private static (Dictionary<string, List<string>> BySimpleName, HashSet<string> Members) Surface()
        {
            var bySimpleName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var members = new HashSet<string>(StringComparer.Ordinal);
            var current = (string)null;

            foreach (var name in new[] { "public-api-heddle.txt", "public-api-heddle-language.txt" })
            foreach (var line in File.ReadAllLines(Path.Combine(RepoRoot(), "src", "Heddle.Tests", "TestTemplate", name)))
            {
                if (line.StartsWith("TYPE ", StringComparison.Ordinal))
                {
                    current = StripSuffixes(line.Substring("TYPE ".Length).Split(' ')[0]);
                    var simple = current.Split('.').Last();
                    if (!bySimpleName.TryGetValue(simple, out var list))
                        bySimpleName[simple] = list = new List<string>();
                    if (!list.Contains(current))
                        list.Add(current);
                    continue;
                }

                if (current == null || !line.StartsWith("  ", StringComparison.Ordinal))
                    continue;

                // "  METHOD System.Void Register(System.Reflection.Assembly)" → Register
                // The declared type may itself contain spaces ("System.Func<A, B, C>"), so anchor on the
                // member name's own terminator instead of on a space-free type token.
                var match = Regex.Match(line,
                    @"^  (?:METHOD|PROP|FIELD|EVENT)\s+.*?(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|\{|$)");
                if (match.Success)
                    members.Add(current + "." + match.Groups["member"].Value);
            }

            Assert.True(bySimpleName.Count > 20,
                "Public-API goldens parsed to only " + bySimpleName.Count + " types.");
            return (bySimpleName, members);
        }

        private static string StripSuffixes(string type)
        {
            var generic = type.IndexOf('<');
            return generic >= 0 ? type.Substring(0, generic) : type;
        }

        private static string RepoRoot([CallerFilePath] string here = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
        }
    }
}
