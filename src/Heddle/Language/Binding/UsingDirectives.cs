using System;
using System.Collections.Generic;

namespace Heddle.Language.Binding
{
    /// <summary>
    /// What a set of collected <c>@using</c> bodies means to <b>type resolution</b>. A body is the header of a C#
    /// using directive, and only one of its three forms opens a namespace: <c>X = Some.Target</c> and
    /// <c>static Some.Target</c> bind a name instead, so neither can be understood by looking for it in a list of
    /// namespace strings. They travel in the same list because that list is also written verbatim into the C# unit
    /// the engine compiles for an embedded expression; this type is the reading of it that type resolution needs.
    /// <para>The classification is <b>syntactic only</b>. Whether an alias target names a namespace or a type — and
    /// whether it names anything at all — is a question for a type universe, and each tier answers it against its
    /// own.</para>
    /// <para>Deliberately not modelled, because nothing here resolves them and pretending otherwise would resolve a
    /// name off a target the C# compiler reads differently: an extern-alias qualifier (<c>A::B</c>), a target
    /// carrying type arguments (<c>X = List&lt;int&gt;</c>), and interior whitespace inside a target
    /// (<c>X = System . Linq</c>). Each is left classified as a plain namespace body, which is inert.</para>
    /// </summary>
    internal sealed class UsingDirectives
    {
        /// <summary>The qualifier that names the global namespace, bypassing every import and alias.</summary>
        internal const string GlobalQualifier = "global::";

        private const string StaticPrefix = "static";

        private static readonly Dictionary<string, string> NoAliases =
            new Dictionary<string, string>(0, StringComparer.Ordinal);

        private static readonly string[] NoTargets = new string[0];

        internal static readonly UsingDirectives None = new UsingDirectives(NoAliases, NoTargets);

        private UsingDirectives(Dictionary<string, string> aliases, IReadOnlyList<string> staticTargets)
        {
            Aliases = aliases;
            StaticTargets = staticTargets;
        }

        /// <summary>Alias name to the target spelling exactly as written, <c>global::</c> included.</summary>
        internal IReadOnlyDictionary<string, string> Aliases { get; }

        /// <summary>The <c>using static</c> targets, in the order the bodies were collected.</summary>
        internal IReadOnlyList<string> StaticTargets { get; }

        internal bool IsEmpty => Aliases.Count == 0 && StaticTargets.Count == 0;

        /// <summary>
        /// Whether an alias claims the <b>head</b> of <paramref name="spelling"/> — the first segment of a dotted
        /// name, or the whole of a simple one.
        /// <para>C# resolves that head through the scope's alias directives before it consults the namespaces the
        /// scope imports, and once an alias claims the head the binding <b>commits</b>: a target that names nothing
        /// is an error, not a fallback to an import. Both tiers ask this one question, so neither can drift from
        /// the other on where the alias arm sits.</para>
        /// </summary>
        internal bool ClaimsHead(string spelling)
        {
            if (spelling == null || Aliases.Count == 0)
                return false;

            var dot = spelling.IndexOf('.');
            return Aliases.ContainsKey(dot < 0 ? spelling : spelling.Substring(0, dot));
        }

        /// <summary>
        /// Reads the alias and <c>static</c> bodies out of <paramref name="bodies"/>; every other body is a plain
        /// namespace import and is left where it is.
        /// <para>A name aliased twice to different targets is dropped rather than picked between: C# refuses the
        /// duplicate outright (CS1537), and choosing one of them by collection order is the order-dependent bind
        /// this resolver already ruled out for short names.</para>
        /// </summary>
        internal static UsingDirectives Parse(IEnumerable<string> bodies)
        {
            if (bodies == null)
                return None;

            Dictionary<string, string> aliases = null;
            List<string> statics = null;
            HashSet<string> duplicated = null;

            foreach (var body in bodies)
            {
                if (TryReadStaticTarget(body, out var staticTarget))
                {
                    (statics ?? (statics = new List<string>())).Add(staticTarget);
                    continue;
                }

                if (!TryReadAlias(body, out var name, out var target))
                    continue;

                aliases = aliases ?? new Dictionary<string, string>(StringComparer.Ordinal);
                if (aliases.TryGetValue(name, out var existing))
                {
                    if (!string.Equals(existing, target, StringComparison.Ordinal))
                        (duplicated ?? (duplicated = new HashSet<string>(StringComparer.Ordinal))).Add(name);
                    continue;
                }

                aliases[name] = target;
            }

            if (duplicated != null)
                foreach (var name in duplicated)
                    aliases.Remove(name);

            if (aliases == null && statics == null)
                return None;

            return new UsingDirectives(aliases ?? NoAliases, (IReadOnlyList<string>) statics ?? NoTargets);
        }

        /// <summary>Takes the <c>global::</c> qualifier off <paramref name="spelling"/>; false when it carries
        /// none, in which case <paramref name="rest"/> is the spelling unchanged.</summary>
        internal static bool TryStripGlobalQualifier(string spelling, out string rest)
        {
            rest = spelling;
            if (spelling == null || spelling.Length <= GlobalQualifier.Length ||
                !spelling.StartsWith(GlobalQualifier, StringComparison.Ordinal))
                return false;

            rest = spelling.Substring(GlobalQualifier.Length).TrimStart();
            return rest.Length != 0;
        }

        /// <summary>The <c>static Some.Target</c> form. The keyword has to be followed by whitespace, so a namespace
        /// whose first segment merely begins with those six letters stays a namespace body.</summary>
        internal static bool TryReadStaticTarget(string body, out string target)
        {
            target = null;
            if (body == null)
                return false;
            var text = body.Trim();
            if (text.Length <= StaticPrefix.Length ||
                !text.StartsWith(StaticPrefix, StringComparison.Ordinal) ||
                !char.IsWhiteSpace(text[StaticPrefix.Length]))
                return false;

            target = text.Substring(StaticPrefix.Length).Trim();
            return target.Length != 0;
        }

        /// <summary>The <c>X = Some.Target</c> form. The left side must be one identifier — a verbatim <c>@X</c>
        /// carries the name <c>X</c>, which is what the C# compiler binds it under.</summary>
        internal static bool TryReadAlias(string body, out string name, out string target)
        {
            name = null;
            target = null;
            if (body == null)
                return false;

            var text = body.Trim();
            var equals = text.IndexOf('=');
            if (equals < 0)
                return false;

            var left = text.Substring(0, equals).Trim();
            if (left.Length != 0 && left[0] == '@')
                left = left.Substring(1);
            if (!IsIdentifier(left))
                return false;

            var right = text.Substring(equals + 1).Trim();
            if (right.Length == 0)
                return false;

            name = left;
            target = right;
            return true;
        }

        private static bool IsIdentifier(string text)
        {
            if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_'))
                return false;
            for (var i = 1; i < text.Length; i++)
                if (!(char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    return false;
            return true;
        }
    }
}
