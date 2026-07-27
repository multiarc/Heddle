using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace Heddle.Language
{
    /// <summary>Raised when a parse exceeds <see cref="ParseDepthGuard.MaxDepth"/>. Catchable, which is the whole
    /// point: the alternative was a <c>StackOverflowException</c>, which is not.</summary>
    internal sealed class ParseDepthExceededException : Exception
    {
        internal ParseDepthExceededException(int depth)
            : base("Parse nesting exceeded " + depth + " levels.")
        {
        }
    }

    /// <summary>
    /// Bounds how deeply a parse may nest.
    /// <para>The generated parser spends one stack frame per rule invocation, and the parts of the expression rule
    /// that recurse rather than loop — prefix operators, and the right-associative <c>??</c> and <c>?:</c> — turn
    /// operator count directly into stack depth. Deep enough input exhausted the stack inside the parser itself,
    /// before any Heddle code was reached, and a <c>StackOverflowException</c> cannot be caught: a single template
    /// file ended the process. Left-associative operators are unaffected, because ANTLR rewrites those into a loop,
    /// which is exactly why only some shapes ever crashed.</para>
    /// <para>The bound is a <b>depth count, not a stack measurement</b>. Probing remaining stack looks equivalent and
    /// is not: the same template then compiles on one host and fails on another, because the answer depends on
    /// thread stack size and on whether the build is optimised. A fixed count behaves the same everywhere, which is
    /// also what makes it testable.</para>
    /// <para>Attached with <see cref="Parser.AddParseListener"/> so the parser is notified as it descends. That needs
    /// no change to the grammar, which matters because the generated parser is committed rather than produced by the
    /// build.</para>
    /// </summary>
    internal sealed class ParseDepthGuard : IParseTreeListener
    {
        /// <summary>
        /// Chosen against the smallest stack the engine can be hosted on, in the configuration it ships in. Both parts
        /// matter and the second was got wrong once: 300 was derived from a Debug build, where a 1 MB thread survives
        /// to 503, while a <b>Release</b> build — what a source generator actually runs as — dies at 284 and the
        /// guard fired at 293, so it could never fire first. Measured last-safe depths, Release on a 1 MB thread:
        /// 284 for prefix operators and <c>?:</c>, 287 for <c>??</c>, 574 for parentheses.
        /// <para>This bounds the parser's own descent. It does <b>not</b> bound ANTLR's prediction, which recurses
        /// separately: a long enough prefix run exhausts the stack inside <c>ParserATNSimulator</c> with the rule
        /// depth still in single figures, at any stack size. No fixed count can see that one.</para>
        /// <para>250 allows about eighty nested blocks — a block costs three rules — against the few dozen a
        /// deeply layered layout reaches. Blocks are the tightest dimension by far and the margin there is roughly
        /// 2.5×, not the order of magnitude it is tempting to claim; every other shape has far more room (around 240
        /// chained operators or indexers, and member paths cost nothing to
        /// <i>parse</i> — though they are not free to compile: a null-safe hop currently duplicates its receiver, so
        /// a long chain is exponential downstream of this guard, which is a separate defect and not one a depth
        /// bound can address).</para>
        /// </summary>
        internal const int MaxDepth = 250;

        private int _depth;

        /// <summary>Clears the count between the two attempts of a two-stage parse, which share this instance.</summary>
        internal void Reset()
        {
            _depth = 0;
        }

        public void EnterEveryRule(ParserRuleContext context)
        {
            if (++_depth > MaxDepth)
                throw new ParseDepthExceededException(MaxDepth);
        }

        public void ExitEveryRule(ParserRuleContext context)
        {
            _depth--;
        }

        public void VisitTerminal(ITerminalNode node)
        {
        }

        public void VisitErrorNode(IErrorNode node)
        {
        }

        /// <summary>
        /// The second half of the bound, and it is needed because the first cannot see this case. A left-associative
        /// operator run is parsed by a loop, so the parser never nests — but it still builds a tree one level deep
        /// per operator, and the tree walk and the AST and chain builders all recurse over <i>that</i>. So
        /// <c>1+1+1…</c> left the parser untouched and overflowed afterwards.
        /// <para>Measured with an explicit stack rather than recursion, since a recursive depth check on an
        /// over-deep tree is the very thing being guarded against.</para>
        /// </summary>
        internal static void EnsureTreeWithinLimit(IParseTree root)
        {
            if (root == null)
                return;

            var pending = new Stack<IParseTree>();
            var depths = new Stack<int>();
            pending.Push(root);
            depths.Push(1);

            while (pending.Count > 0)
            {
                var node = pending.Pop();
                var depth = depths.Pop();
                if (depth > MaxDepth)
                    throw new ParseDepthExceededException(MaxDepth);

                for (var i = 0; i < node.ChildCount; i++)
                {
                    pending.Push(node.GetChild(i));
                    depths.Push(depth + 1);
                }
            }
        }
    }
}
