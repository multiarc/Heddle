using System.Collections.Generic;
using System.Text;

namespace Heddle.Tool.Compile
{
    /// <summary>The entry-class name rule, kept identical to the build tier it replaces: per
    /// <c>/</c> segment (extension dropped from the last), skip empty segments, upper-case the first
    /// character, keep <c>_</c>/letters/digits (a leading digit gets <c>_</c> prefixed), replace every
    /// other character with <c>_</c>, join segments with <c>_</c>.</summary>
    internal static class SanitizeName
    {
        internal static string ForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "_";
            var lastSlash = key.LastIndexOf('/');
            var dir = lastSlash >= 0 ? key.Substring(0, lastSlash) : string.Empty;
            var file = lastSlash >= 0 ? key.Substring(lastSlash + 1) : key;
            var dot = file.LastIndexOf('.');
            if (dot > 0)
                file = file.Substring(0, dot);

            var segments = new List<string>();
            if (dir.Length != 0)
                segments.AddRange(dir.Split('/'));
            segments.Add(file);

            var parts = new List<string>();
            foreach (var seg in segments)
            {
                if (seg.Length == 0)
                    continue;
                var sb = new StringBuilder(seg.Length);
                for (int i = 0; i < seg.Length; i++)
                {
                    var c = seg[i];
                    bool valid = c == '_' || char.IsLetter(c) || (i > 0 && char.IsDigit(c));
                    if (i == 0 && char.IsDigit(c))
                        sb.Append('_').Append(c);
                    else if (valid)
                        sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
                    else
                        sb.Append('_');
                }

                parts.Add(sb.ToString());
            }

            var result = string.Join("_", parts);
            return result.Length == 0 ? "_" : result;
        }
    }
}
