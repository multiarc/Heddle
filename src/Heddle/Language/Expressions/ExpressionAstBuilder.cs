using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Heddle.Strings.Core;

namespace Heddle.Language.Expressions
{
    /// <summary>
    /// Converts the ANTLR <c>expr</c> parse tree into the public <see cref="ExprNode"/> AST, decoding literals
    /// and emitting editor tokens through <see cref="ParseContext.AddToken(IToken, HeddleTokenType)"/>.
    /// </summary>
    internal static class ExpressionAstBuilder
    {
        internal static ExprNode Build(HeddleParser.Native_expressionContext context, ParseContext parseContext)
        {
            if (context?.expr() == null)
                return null;
            return Build(context.expr(), parseContext);
        }

        internal static ExprNode Build(HeddleParser.ExprContext ctx, ParseContext parseContext)
        {
            switch (ctx)
            {
                case HeddleParser.ThisExprContext thisExpr:
                    parseContext.AddToken(thisExpr.THIS(), HeddleTokenType.Literal);
                    return new ThisNode(Span(ctx));

                case HeddleParser.GroupExprContext group:
                    parseContext.AddToken(group.OUT_PARAMSTART(), HeddleTokenType.OutParamStart);
                    parseContext.AddToken(group.OUT_PARAMEND(), HeddleTokenType.OutParamEnd);
                    return Build(group.expr(), parseContext);

                case HeddleParser.LiteralExprContext literalExpr:
                    return BuildLiteral(literalExpr.literal(), parseContext);

                case HeddleParser.PathRootExprContext pathRoot:
                {
                    bool rootRef = pathRoot.ROOT_REF() != null;
                    parseContext.AddToken(pathRoot.ROOT_REF(), HeddleTokenType.RootReference);
                    parseContext.AddToken(pathRoot.ID(), HeddleTokenType.Id);
                    var segment = pathRoot.ID().GetText().Trim();
                    return new PathNode(rootRef, new[] { segment }, null, Span(ctx));
                }

                case HeddleParser.MemberHopExprContext hop:
                {
                    var inner = Build(hop.expr(), parseContext);
                    parseContext.AddToken(hop.MEMBER_P(), HeddleTokenType.MemberSelector);
                    parseContext.AddToken(hop.ID(), HeddleTokenType.Id);
                    var id = hop.ID().GetText().Trim();
                    if (inner is PathNode p)
                    {
                        var segments = new List<string>(p.Segments) { id };
                        return new PathNode(p.RootRef, segments, p.Target, Span(ctx));
                    }

                    return new PathNode(false, new[] { id }, inner, Span(ctx));
                }

                case HeddleParser.MethodCallExprContext method:
                {
                    var target = Build(method.expr(), parseContext);
                    parseContext.AddToken(method.MEMBER_P(), HeddleTokenType.MemberSelector);
                    parseContext.AddToken(method.ID(), HeddleTokenType.FunctionName);
                    var args = BuildArgList(method.arg_list(), parseContext);
                    return new MethodCallNode(target, method.ID().GetText().Trim(), args, Span(ctx));
                }

                case HeddleParser.IndexExprContext index:
                {
                    var exprs = index.expr();
                    var target = Build(exprs[0], parseContext);
                    parseContext.AddToken(index.LBRACKET(), HeddleTokenType.Operator);
                    parseContext.AddToken(index.RBRACKET(), HeddleTokenType.Operator);
                    foreach (var comma in index.COMMA())
                        parseContext.AddToken(comma, HeddleTokenType.Operator);
                    var args = new List<ExprNode>();
                    for (int i = 1; i < exprs.Length; i++)
                        args.Add(Build(exprs[i], parseContext));
                    return new IndexNode(target, args, Span(ctx));
                }

                case HeddleParser.FunctionCallExprContext call:
                {
                    parseContext.AddToken(call.ID(), HeddleTokenType.FunctionName);
                    var args = BuildArgList(call.arg_list(), parseContext);
                    return new CallNode(call.ID().GetText().Trim(), args, Span(ctx));
                }

                case HeddleParser.UnaryExprContext unary:
                {
                    parseContext.AddToken(unary.op, HeddleTokenType.Operator);
                    var operand = Build(unary.expr(), parseContext);
                    return new UnaryNode(MapUnary(unary.op.Type), operand, Span(ctx));
                }

                case HeddleParser.MultiplicativeExprContext mul:
                    return BuildBinary(mul.expr(0), mul.expr(1), mul.op, MapBinary(mul.op.Type), parseContext, ctx);

                case HeddleParser.AdditiveExprContext add:
                    return BuildBinary(add.expr(0), add.expr(1), add.op, MapBinary(add.op.Type), parseContext, ctx);

                case HeddleParser.ShiftExprContext shift:
                    return BuildBinary(shift.expr(0), shift.expr(1), shift.op, MapBinary(shift.op.Type), parseContext, ctx);

                case HeddleParser.RelationalExprContext rel:
                    return BuildBinary(rel.expr(0), rel.expr(1), rel.op, MapBinary(rel.op.Type), parseContext, ctx);

                case HeddleParser.EqualityExprContext eq:
                    return BuildBinary(eq.expr(0), eq.expr(1), eq.op, MapBinary(eq.op.Type), parseContext, ctx);

                case HeddleParser.BitAndExprContext bitAnd:
                    return BuildBinary(bitAnd.expr(0), bitAnd.expr(1), bitAnd.OP_AMP().Symbol, ExprOperator.And, parseContext, ctx);

                case HeddleParser.BitXorExprContext bitXor:
                    return BuildBinary(bitXor.expr(0), bitXor.expr(1), bitXor.OP_CARET().Symbol, ExprOperator.ExclusiveOr, parseContext, ctx);

                case HeddleParser.BitOrExprContext bitOr:
                    return BuildBinary(bitOr.expr(0), bitOr.expr(1), bitOr.OP_PIPE().Symbol, ExprOperator.Or, parseContext, ctx);

                case HeddleParser.AndAlsoExprContext andAlso:
                    return BuildBinary(andAlso.expr(0), andAlso.expr(1), andAlso.OP_AND().Symbol, ExprOperator.AndAlso, parseContext, ctx);

                case HeddleParser.OrElseExprContext orElse:
                    return BuildBinary(orElse.expr(0), orElse.expr(1), orElse.OP_OR().Symbol, ExprOperator.OrElse, parseContext, ctx);

                case HeddleParser.CoalesceExprContext coalesce:
                    return BuildBinary(coalesce.expr(0), coalesce.expr(1), coalesce.OP_QQ().Symbol, ExprOperator.Coalesce, parseContext, ctx);

                case HeddleParser.TernaryExprContext ternary:
                {
                    var condition = Build(ternary.expr(0), parseContext);
                    var whenTrue = Build(ternary.expr(1), parseContext);
                    var whenFalse = Build(ternary.expr(2), parseContext);
                    parseContext.AddToken(ternary.OP_QUESTION(), HeddleTokenType.Operator);
                    parseContext.AddToken(ternary.DELIM(), HeddleTokenType.Operator);
                    return new TernaryNode(condition, whenTrue, whenFalse, Span(ctx));
                }
            }

            return null;
        }

        private static ExprNode BuildBinary(HeddleParser.ExprContext left, HeddleParser.ExprContext right, IToken opToken,
            ExprOperator op, ParseContext parseContext, HeddleParser.ExprContext ctx)
        {
            var leftNode = Build(left, parseContext);
            var rightNode = Build(right, parseContext);
            parseContext.AddToken(opToken, HeddleTokenType.Operator);
            return new BinaryNode(op, leftNode, rightNode, Span(ctx));
        }

        private static List<ExprNode> BuildArgList(HeddleParser.Arg_listContext argList, ParseContext parseContext)
        {
            var result = new List<ExprNode>();
            if (argList == null)
                return result;
            parseContext.AddToken(argList.OUT_PARAMSTART(), HeddleTokenType.OutParamStart);
            parseContext.AddToken(argList.OUT_PARAMEND(), HeddleTokenType.OutParamEnd);
            foreach (var comma in argList.COMMA())
                parseContext.AddToken(comma, HeddleTokenType.Operator);
            foreach (var expr in argList.expr())
                result.Add(Build(expr, parseContext));
            return result;
        }

        private static LiteralNode BuildLiteral(HeddleParser.LiteralContext literal, ParseContext parseContext)
        {
            ITerminalNode terminal =
                literal.INT_LIT() ?? literal.REAL_LIT() ?? literal.STRING_LIT() ?? literal.CHAR_LIT() ??
                literal.TRUE() ?? literal.FALSE() ?? literal.NULL();
            parseContext.AddToken(terminal, HeddleTokenType.Literal);
            var position = Span(literal);

            if (literal.TRUE() != null)
                return new LiteralNode(true, position);
            if (literal.FALSE() != null)
                return new LiteralNode(false, position);
            if (literal.NULL() != null)
                return new LiteralNode(null, position);
            if (literal.STRING_LIT() != null)
                return new LiteralNode(DecodeString(literal.STRING_LIT().GetText()), position);
            if (literal.CHAR_LIT() != null)
                return new LiteralNode(DecodeChar(literal.CHAR_LIT().GetText()), position);
            if (literal.INT_LIT() != null)
                return DecodeInteger(literal.INT_LIT().GetText(), position);
            return DecodeReal(literal.REAL_LIT().GetText(), position);
        }

        /// <summary>
        /// Decodes a <c>def_literal</c> (a prop default) into its pre-conversion boxed CLR value, reusing the
        /// same literal-decoding routines the expression builder uses (the D2 DRY move). <paramref name="isNull"/>
        /// is true for the <c>null</c> literal (the returned value is then <c>null</c>). Emits the default's
        /// editor tokens through <paramref name="parseContext"/>.
        /// </summary>
        internal static object DecodeDefaultLiteral(HeddleParser.Def_literalContext literal, ParseContext parseContext,
            out bool isNull)
        {
            isNull = false;
            var minus = literal.OP_MINUS();
            if (minus != null)
                parseContext.AddToken(minus, HeddleTokenType.Operator);

            if (literal.TRUE() != null)
            {
                parseContext.AddToken(literal.TRUE(), HeddleTokenType.Literal);
                return true;
            }
            if (literal.FALSE() != null)
            {
                parseContext.AddToken(literal.FALSE(), HeddleTokenType.Literal);
                return false;
            }
            if (literal.NULL() != null)
            {
                parseContext.AddToken(literal.NULL(), HeddleTokenType.Literal);
                isNull = true;
                return null;
            }
            if (literal.STRING_LIT() != null)
            {
                parseContext.AddToken(literal.STRING_LIT(), HeddleTokenType.Literal);
                return DecodeString(literal.STRING_LIT().GetText());
            }
            if (literal.CHAR_LIT() != null)
            {
                parseContext.AddToken(literal.CHAR_LIT(), HeddleTokenType.Literal);
                return DecodeChar(literal.CHAR_LIT().GetText());
            }
            if (literal.INT_LIT() != null)
            {
                parseContext.AddToken(literal.INT_LIT(), HeddleTokenType.Literal);
                var node = DecodeInteger(literal.INT_LIT().GetText(), default);
                return minus != null ? Negate(node.Value) : node.Value;
            }

            parseContext.AddToken(literal.REAL_LIT(), HeddleTokenType.Literal);
            var real = DecodeReal(literal.REAL_LIT().GetText(), default);
            return minus != null ? Negate(real.Value) : real.Value;
        }

        private static object Negate(object value)
        {
            switch (value)
            {
                case int i: return -i;
                case long l: return -l;
                case uint u: return -(long) u;
                case ulong ul: return -(decimal) ul;
                case float f: return -f;
                case double d: return -d;
                case decimal m: return -m;
                default: return value;
            }
        }

        private static ExprOperator MapUnary(int tokenType)
        {
            if (tokenType == HeddleParser.OP_NOT) return ExprOperator.Not;
            if (tokenType == HeddleParser.OP_MINUS) return ExprOperator.Negate;
            if (tokenType == HeddleParser.OP_PLUS) return ExprOperator.UnaryPlus;
            return ExprOperator.OnesComplement; // OP_TILDE
        }

        private static ExprOperator MapBinary(int tokenType)
        {
            if (tokenType == HeddleParser.OP_STAR) return ExprOperator.Multiply;
            if (tokenType == HeddleParser.OP_SLASH) return ExprOperator.Divide;
            if (tokenType == HeddleParser.OP_PERCENT) return ExprOperator.Modulo;
            if (tokenType == HeddleParser.OP_PLUS) return ExprOperator.Add;
            if (tokenType == HeddleParser.OP_MINUS) return ExprOperator.Subtract;
            if (tokenType == HeddleParser.OP_LSHIFT) return ExprOperator.LeftShift;
            if (tokenType == HeddleParser.OP_RSHIFT) return ExprOperator.RightShift;
            if (tokenType == HeddleParser.OP_LT) return ExprOperator.LessThan;
            if (tokenType == HeddleParser.OP_GT) return ExprOperator.GreaterThan;
            if (tokenType == HeddleParser.OP_LE) return ExprOperator.LessThanOrEqual;
            if (tokenType == HeddleParser.OP_GE) return ExprOperator.GreaterThanOrEqual;
            if (tokenType == HeddleParser.OP_EQ) return ExprOperator.Equal;
            return ExprOperator.NotEqual; // OP_NEQ
        }

        private static BlockPosition Span(ParserRuleContext ctx)
        {
            return new BlockPosition(ctx.Start.StartIndex, ctx.Stop.StopIndex - ctx.Start.StartIndex + 1);
        }

        #region Literal decoding

        private static LiteralNode DecodeInteger(string text, BlockPosition position)
        {
            string body = text;
            int suffixLength = 0;
            while (suffixLength < body.Length)
            {
                char c = body[body.Length - 1 - suffixLength];
                if (c == 'u' || c == 'U' || c == 'l' || c == 'L')
                    suffixLength++;
                else
                    break;
            }

            string suffix = body.Substring(body.Length - suffixLength).ToLowerInvariant();
            string digits = body.Substring(0, body.Length - suffixLength).Replace("_", string.Empty);
            bool hasU = suffix.IndexOf('u') >= 0;
            bool hasL = suffix.IndexOf('l') >= 0;

            ulong value;
            try
            {
                if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    value = ParseRadix(digits.Substring(2), 16);
                else if (digits.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                    value = ParseRadix(digits.Substring(2), 2);
                else
                    value = ulong.Parse(digits, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                return OutOfRange(position);
            }
            catch (FormatException)
            {
                return OutOfRange(position);
            }

            object typed;
            if (hasU && hasL)
                typed = value;
            else if (hasU)
                typed = value <= uint.MaxValue ? (object)(uint)value : value;
            else if (hasL)
                typed = value <= long.MaxValue ? (object)(long)value : value;
            else if (value <= int.MaxValue)
                typed = (int)value;
            else if (value <= uint.MaxValue)
                typed = (uint)value;
            else if (value <= long.MaxValue)
                typed = (long)value;
            else
                typed = value;

            return new LiteralNode(typed, position);
        }

        private static ulong ParseRadix(string digits, int radix)
        {
            if (digits.Length == 0)
                throw new FormatException();
            ulong result = 0;
            foreach (char c in digits)
            {
                int digit;
                if (c >= '0' && c <= '9') digit = c - '0';
                else if (c >= 'a' && c <= 'f') digit = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') digit = c - 'A' + 10;
                else throw new FormatException();
                if (digit >= radix)
                    throw new FormatException();
                checked
                {
                    result = result * (ulong)radix + (ulong)digit;
                }
            }

            return result;
        }

        private static LiteralNode DecodeReal(string text, BlockPosition position)
        {
            string body = text.Replace("_", string.Empty);
            char last = body[body.Length - 1];
            try
            {
                if (last == 'f' || last == 'F')
                {
                    float f = float.Parse(body.Substring(0, body.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (float.IsInfinity(f))
                        return OutOfRange(position);
                    return new LiteralNode(f, position);
                }

                if (last == 'd' || last == 'D')
                {
                    double d = double.Parse(body.Substring(0, body.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (double.IsInfinity(d))
                        return OutOfRange(position);
                    return new LiteralNode(d, position);
                }

                if (last == 'm' || last == 'M')
                {
                    decimal m = decimal.Parse(body.Substring(0, body.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture);
                    return new LiteralNode(m, position);
                }

                double value = double.Parse(body, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (double.IsInfinity(value))
                    return OutOfRange(position);
                return new LiteralNode(value, position);
            }
            catch (OverflowException)
            {
                return OutOfRange(position);
            }
        }

        private static LiteralNode OutOfRange(BlockPosition position)
        {
            return new LiteralNode(0, position) { LiteralError = "Literal is out of range of every numeric literal type." };
        }

        private static string DecodeString(string text)
        {
            // Strip the surrounding double quotes.
            string body = text.Substring(1, text.Length - 2);
            return DecodeEscapes(body, false);
        }

        private static char DecodeChar(string text)
        {
            string body = text.Substring(1, text.Length - 2);
            string decoded = DecodeEscapes(body, true);
            return decoded.Length > 0 ? decoded[0] : '\0';
        }

        private static string DecodeEscapes(string body, bool isChar)
        {
            var sb = new StringBuilder(body.Length);
            int i = 0;
            while (i < body.Length)
            {
                char c = body[i];
                if (c != '\\')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                i++;
                if (i >= body.Length)
                    break;
                char e = body[i];
                switch (e)
                {
                    case '\'': sb.Append('\''); i++; break;
                    case '"': sb.Append('"'); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case '0': sb.Append('\0'); i++; break;
                    case 'a': sb.Append('\a'); i++; break;
                    case 'b': sb.Append('\b'); i++; break;
                    case 'e': sb.Append((char)0x1b); i++; break;
                    case 'f': sb.Append('\f'); i++; break;
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    case 'v': sb.Append('\v'); i++; break;
                    case 'x':
                    {
                        i++;
                        int start = i;
                        int count = 0;
                        while (i < body.Length && count < 4 && Uri.IsHexDigit(body[i]))
                        {
                            i++;
                            count++;
                        }

                        if (count > 0)
                            sb.Append((char)Convert.ToInt32(body.Substring(start, count), 16));
                        break;
                    }
                    case 'u':
                    {
                        i++;
                        if (i + 4 <= body.Length)
                        {
                            sb.Append((char)Convert.ToInt32(body.Substring(i, 4), 16));
                            i += 4;
                        }

                        break;
                    }
                    case 'U':
                    {
                        i++;
                        if (i + 8 <= body.Length)
                        {
                            int codePoint = Convert.ToInt32(body.Substring(i, 8), 16);
                            sb.Append(char.ConvertFromUtf32(codePoint));
                            i += 8;
                        }

                        break;
                    }
                    default:
                        sb.Append(e);
                        i++;
                        break;
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
