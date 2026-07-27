using System;
using Heddle.Language.Expressions;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Whether C# would refuse to compile an expression that the dynamic engine accepts.
    /// <para>The two tiers disagree about when an arithmetic fault is discovered. The engine builds a
    /// <c>System.Linq.Expressions</c> tree and finds out at render time — <c>@(1/0)</c> compiles and throws when
    /// rendered, and <c>@(2147483647+1)</c> compiles and renders <c>-2147483648</c>. C# decides both at compile time
    /// and rejects them outright, so writing the operator through verbatim turned a template the engine renders into
    /// a build error in the host's project, reported against the <c>.heddle</c> file with no Heddle diagnostic
    /// attached.</para>
    /// <para>Rather than guess a value, an expression identified here is left unwritten, which degrades it to the
    /// dynamic tier — the tier whose behaviour is the contract. The fold is deliberately timid: anything it cannot
    /// evaluate with certainty is reported as not constant, which emits exactly as before.</para>
    /// </summary>
    internal static class ConstantFolding
    {
        internal static bool CompilerWouldReject(ExprNode node)
        {
            return Fold(node).Rejected;
        }

        private static Folded Fold(ExprNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return IsFoldable(literal.Value) ? Folded.Constant(literal.Value) : Folded.Unknown;
                case UnaryNode unary:
                    return FoldUnary(unary);
                case BinaryNode binary:
                    return FoldBinary(binary);
                default:
                    return Folded.Unknown;
            }
        }

        private static Folded FoldUnary(UnaryNode node)
        {
            var operand = Fold(node.Operand);
            if (!operand.IsConstant)
                return operand;

            if (node.Operator != ExprOperator.Negate)
                return Folded.Unknown;

            // -(-2147483648) is the one negation C# rejects: the result leaves the operand's own range.
            var value = AsInteger(operand.Value);
            if (value == null)
                return Folded.Unknown;
            return Fits(-value.Value, operand.Value) ? Folded.Constant(-value.Value) : Folded.Refused;
        }

        private static Folded FoldBinary(BinaryNode node)
        {
            var left = Fold(node.Left);
            var right = Fold(node.Right);
            if (!left.IsConstant || !right.IsConstant)
                return left.Rejected || right.Rejected ? Folded.Refused : Folded.Unknown;

            if (node.Operator == ExprOperator.Divide || node.Operator == ExprOperator.Modulo)
                return DividesByZero(right.Value) ? Folded.Refused : Folded.Unknown;

            var a = AsInteger(left.Value);
            var b = AsInteger(right.Value);
            if (a == null || b == null)
                return Folded.Unknown;

            long result;
            try
            {
                switch (node.Operator)
                {
                    case ExprOperator.Add: result = checked(a.Value + b.Value); break;
                    case ExprOperator.Subtract: result = checked(a.Value - b.Value); break;
                    case ExprOperator.Multiply: result = checked(a.Value * b.Value); break;
                    default: return Folded.Unknown;
                }
            }
            catch (OverflowException)
            {
                return Folded.Refused;   // beyond long itself, so beyond every promoted type too
            }

            return Fits(result, left.Value) && Fits(result, right.Value)
                ? Folded.Constant(result)
                : Folded.Refused;
        }

        /// <summary>Zero divisors C# rejects outright. Floating point is excluded on purpose: <c>1.0/0</c> is a legal
        /// constant expression yielding infinity, and both tiers agree on it.</summary>
        private static bool DividesByZero(object divisor)
        {
            switch (divisor)
            {
                case sbyte v: return v == 0;
                case byte v: return v == 0;
                case short v: return v == 0;
                case ushort v: return v == 0;
                case int v: return v == 0;
                case uint v: return v == 0;
                case long v: return v == 0;
                case ulong v: return v == 0;
                case decimal v: return v == 0m;
                default: return false;
            }
        }

        private static bool IsFoldable(object value)
        {
            return AsInteger(value) != null || value is decimal || value is double || value is float;
        }

        private static long? AsInteger(object value)
        {
            switch (value)
            {
                case sbyte v: return v;
                case byte v: return v;
                case short v: return v;
                case ushort v: return v;
                case int v: return v;
                case uint v: return v;
                case long v: return v;
                case ulong v: return v <= long.MaxValue ? (long?)v : null;
                default: return null;
            }
        }

        /// <summary>Whether a folded result still fits the operand type C# would promote this operation to. Types
        /// narrower than <c>int</c> promote to <c>int</c>, which is why they share its range here.</summary>
        private static bool Fits(long result, object operand)
        {
            switch (operand)
            {
                case long _:
                case ulong _:
                    return true;
                case uint _:
                    return result >= 0;
                default:
                    return result >= int.MinValue && result <= int.MaxValue;
            }
        }

        private readonly struct Folded
        {
            private Folded(bool isConstant, bool rejected, object value)
            {
                IsConstant = isConstant;
                Rejected = rejected;
                Value = value;
            }

            internal bool IsConstant { get; }
            internal bool Rejected { get; }
            internal object Value { get; }

            internal static Folded Unknown => new Folded(false, false, null);
            internal static Folded Refused => new Folded(false, true, null);
            internal static Folded Constant(object value) => new Folded(true, false, value);
        }
    }
}
