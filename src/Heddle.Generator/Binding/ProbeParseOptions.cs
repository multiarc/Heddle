using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The parse options a probe tree has to carry to be grafted onto the consumer's compilation.
    /// <para>Roslyn requires every tree in a compilation to have been parsed alike and rejects the whole graft
    /// otherwise ("Inconsistent syntax tree features"), so a probe parsed with the defaults throws for every
    /// consumer whose project sets a language version, a preprocessor symbol or a compiler feature — which the
    /// .NET SDK does on its own. Reading the options off a tree the compilation already holds is what makes the
    /// graft valid, and it is also the only reading that asks the probe's question the way the consumer's own
    /// compiler will answer it.</para>
    /// </summary>
    internal static class ProbeParseOptions
    {
        /// <summary>The options of the first tree in <paramref name="compilation"/>, or the defaults when it holds
        /// no trees — in which case there is nothing for a probe tree to be inconsistent with.</summary>
        internal static CSharpParseOptions For(Compilation compilation) =>
            compilation?.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions ?? CSharpParseOptions.Default;
    }
}
