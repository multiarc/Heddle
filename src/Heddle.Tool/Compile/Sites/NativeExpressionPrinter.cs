using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Heddle.Language.Expressions;

namespace Heddle.Tool.Compile.Sites
{
    /// <summary>Prints one native-expression site from the typed bound tree the engine
    /// compiled: every operand cast to its promoted CLR type is already a <c>Convert</c> in the
    /// tree, so C#'s own promotion never decides; the chosen overload is the <c>MethodCall</c>
    /// method; hop forms are the <c>Condition</c> shapes. A bound node kind with no arm here is
    /// declined, never guessed.</summary>
    internal sealed class NativeExpressionPrinter
    {
        private readonly List<string> _statements = new List<string>();
        private readonly Dictionary<ParameterExpression, string> _locals =
            new Dictionary<ParameterExpression, string>();
        private readonly Dictionary<ParameterExpression, Expression> _pending =
            new Dictionary<ParameterExpression, Expression>();
        private int _localCount;

        private NativeExpressionPrinter()
        {
        }

        internal static bool TryPrint(Expression bound, bool usesProps, string methodName,
            out string code, out string why)
        {
            code = null;
            why = null;
            if (bound == null)
            {
                why = "no bound tree recorded";
                return false;
            }

            var printer = new NativeExpressionPrinter();
            string body;
            string failure;
            if (!printer.Print(bound, out body, out failure))
            {
                why = failure;
                return false;
            }

            // The body already has the bound result type; the return boxes it exactly once
            // through the object return type, mirroring the engine's Convert(body, object).
            string resultCast = body;

            var sb = new StringBuilder();
            if (usesProps)
                sb.Append("        private static object ").Append(methodName)
                    .Append("(object model, object chained, object root, object[] props)\n");
            else
                sb.Append("        private static object ").Append(methodName)
                    .Append("(object model, object chained, object root)\n");
            sb.Append("        {\n");
            foreach (var statement in printer._statements)
                sb.Append("            ").Append(statement).Append('\n');
            // The engine's arithmetic and conversions never check for overflow; the consumer's project may
            // (CheckForOverflowUnderflow), and C# checks constant sub-expressions regardless.
            sb.Append("            return unchecked(").Append(resultCast).Append(");\n");
            sb.Append("        }\n");
            code = sb.ToString();
            return true;
        }

        private bool Print(Expression node, out string text, out string why)
        {
            text = null;
            why = null;
            if (node == null)
            {
                why = "null bound node";
                return false;
            }

            var constant = node as ConstantExpression;
            if (constant != null)
                return PrintConstant(constant, out text, out why);
            if (!(node is DefaultExpression) && IsConstantSubtree(node))
                return PrintFolded(node, out text, out why);
            var parameter = node as ParameterExpression;
            if (parameter != null)
                return PrintParameter(parameter, out text, out why);
            var unary = node as UnaryExpression;
            if (unary != null)
                return PrintUnary(unary, out text, out why);
            var binary = node as BinaryExpression;
            if (binary != null)
                return PrintBinary(binary, out text, out why);
            var methodCall = node as MethodCallExpression;
            if (methodCall != null)
                return PrintMethodCall(methodCall, out text, out why);
            var member = node as MemberExpression;
            if (member != null)
                return PrintMember(member, out text, out why);
            var index = node as IndexExpression;
            if (index != null)
                return PrintIndex(index, out text, out why);
            var conditional = node as ConditionalExpression;
            if (conditional != null)
                return PrintConditional(conditional, out text, out why);
            var block = node as BlockExpression;
            if (block != null)
                return PrintBlock(block, out text, out why);
            var newArray = node as NewArrayExpression;
            if (newArray != null)
                return PrintNewArray(newArray, out text, out why);
            var @default = node as DefaultExpression;
            if (@default != null)
                return PrintDefault(@default, out text, out why);
            var invocation = node as InvocationExpression;
            if (invocation != null)
            {
                why = "delegate-target call";
                return false;
            }

            why = "unsupported bound node '" + node.NodeType + "'";
            return false;
        }

        /// <summary>Whether the subtree is built from constants and the engine's own pure operators alone — no
        /// parameter, member, indexer or function call, so evaluating it here observes nothing.</summary>
        private static bool IsConstantSubtree(Expression node)
        {
            if (node is ConstantExpression || node is DefaultExpression)
                return true;
            var unary = node as UnaryExpression;
            if (unary != null)
                return IsPureOperator(unary.Method) && IsConstantSubtree(unary.Operand);
            var binary = node as BinaryExpression;
            if (binary != null)
                return binary.NodeType != ExpressionType.Assign && binary.Conversion == null &&
                    IsPureOperator(binary.Method) && !FormatsAtRender(binary) &&
                    IsConstantSubtree(binary.Left) && IsConstantSubtree(binary.Right);
            var conditional = node as ConditionalExpression;
            return conditional != null && IsConstantSubtree(conditional.Test) &&
                IsConstantSubtree(conditional.IfTrue) && IsConstantSubtree(conditional.IfFalse);
        }

        /// <summary>Whether a concatenation turns a non-text operand into text. It does so when it runs, under
        /// the culture of the render — <c>"x" + 1.5</c> is <c>x1,5</c> in German — so its value is not a
        /// constant of the template, and evaluating it here would bake in the build machine's culture.</summary>
        private static bool FormatsAtRender(BinaryExpression node)
        {
            return IsStringConcat(node.Method) && (FormatsOperand(node.Left) || FormatsOperand(node.Right));
        }

        private static bool FormatsOperand(Expression operand)
        {
            while (operand.NodeType == ExpressionType.Convert && operand.Type == typeof(object))
                operand = ((UnaryExpression)operand).Operand;
            return operand.Type != typeof(string) && operand.Type != typeof(char) &&
                !(operand is ConstantExpression && ((ConstantExpression)operand).Value == null);
        }

        /// <summary>No method, or one of the core library's own operators (decimal arithmetic, string
        /// concatenation and equality): never a user-defined operator, whose body is not ours to run.</summary>
        private static bool IsPureOperator(MethodInfo method) =>
            method == null || method.DeclaringType.Assembly == typeof(object).Assembly;

        private static bool TryEvaluate(Expression node, out object value, out string why)
        {
            value = null;
            why = null;
            try
            {
                value = Expression.Lambda<Func<object>>(Expression.Convert(node, typeof(object))).Compile()();
                return true;
            }
            catch (Exception ex)
            {
                why = "a constant sub-expression throws " + ex.GetType().Name + " when evaluated";
                return false;
            }
        }

        /// <summary>Prints a constant subtree as the value the engine's own evaluation gives it. Left as
        /// source, the C# compiler would fold it again by C#'s rules, which are not the engine's: constant
        /// <c>decimal</c> overflow is a compile error even under <c>unchecked</c>, and <c>int.MinValue / -1</c>
        /// folds to a value where the engine throws. A subtree that throws here throws at render on the
        /// dynamic tier too — but only if it is reached — so there is no literal to print and the site is
        /// declined.</summary>
        private bool PrintFolded(Expression node, out string text, out string why)
        {
            text = null;
            object value;
            if (!TryEvaluate(node, out value, out why))
                return false;
            var underlying = Nullable.GetUnderlyingType(node.Type);
            if (value == null || underlying == null)
                return PrintConstant(Expression.Constant(value, node.Type), out text, out why);

            string literal;
            if (!TrySpellLiteral(value, underlying, out literal, out why))
                return false;
            string lifted;
            if (!TypeNamePrinter.TrySpell(node.Type, out lifted, out why))
                return false;
            text = "(" + lifted + ")(" + literal + ")";
            return true;
        }

        /// <summary>Whether an integral or decimal division has a constant divisor of zero. C# refuses to
        /// compile one (CS0020) whatever the dividend; the engine compiles it and throws when it runs.</summary>
        private static bool DividesByConstantZero(BinaryExpression node)
        {
            if (node.NodeType != ExpressionType.Divide && node.NodeType != ExpressionType.Modulo)
                return false;
            var type = Nullable.GetUnderlyingType(node.Type) ?? node.Type;
            if (type == typeof(float) || type == typeof(double) || !IsConstantSubtree(node.Right))
                return false;
            object divisor;
            string ignored;
            if (!TryEvaluate(node.Right, out divisor, out ignored) || !(divisor is IConvertible))
                return false;
            try
            {
                return Convert.ToDecimal(divisor, CultureInfo.InvariantCulture) == 0m;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                return false;
            }
        }

        private bool PrintConstant(ConstantExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            if (node.Value == null)
            {
                if (node.Type == typeof(object))
                {
                    text = "null";
                    return true;
                }

                string nullSpelling;
                string nullWhy;
                if (!TypeNamePrinter.TrySpell(node.Type, out nullSpelling, out nullWhy))
                {
                    why = "null constant type: " + nullWhy;
                    return false;
                }

                text = "default(" + nullSpelling + ")";
                return true;
            }

            string literal;
            if (!TrySpellLiteral(node.Value, node.Type, out literal, out why))
                return false;
            text = literal;
            return true;
        }

        internal static bool TrySpellLiteral(object value, Type type, out string literal, out string why)
        {
            literal = null;
            why = null;
            if (value is float || value is double)
            {
                double real = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(real) || double.IsInfinity(real))
                {
                    why = value is float ? "non-finite float constant" : "non-finite double constant";
                    return false;
                }
            }

            // One literal table for the whole engine: the escape rules and the round-trip real formats
            // are the shared formatter's, so a printed constant decodes to the value the engine bound.
            string shared = type.IsEnum ? null : LiteralFormatter.Format(value);
            if (shared != null)
            {
                literal = shared;
                return true;
            }

            if (value is short || value is ushort || value is sbyte || value is byte)
            {
                string spelling;
                string spellWhy;
                if (!TypeNamePrinter.TrySpell(type, out spelling, out spellWhy))
                {
                    why = "small integral constant type: " + spellWhy;
                    return false;
                }

                literal = "(" + spelling + ")" +
                    Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (type.IsEnum)
            {
                string enumSpelling;
                string enumWhy;
                if (!TypeNamePrinter.TrySpell(type, out enumSpelling, out enumWhy))
                {
                    why = "enum constant type: " + enumWhy;
                    return false;
                }

                var underlying = Enum.GetUnderlyingType(type);
                object numeric = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                string numericText;
                string numericWhy;
                if (!TrySpellLiteral(numeric, underlying, out numericText, out numericWhy))
                {
                    why = numericWhy;
                    return false;
                }

                // A signed operand of a cast to a named type must be grouped: `(E)-1` parses as a
                // subtraction from `E`.
                literal = "(" + enumSpelling + ")" +
                    (numericText.Length != 0 && numericText[0] == '-' ? "(" + numericText + ")" : numericText);
                return true;
            }

            why = "constant of type '" + (type.FullName ?? type.Name) + "'";
            return false;
        }

        private bool PrintParameter(ParameterExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            string local;
            if (_locals.TryGetValue(node, out local))
            {
                Expression binding;
                if (!_pending.TryGetValue(node, out binding))
                {
                    text = local;
                    return true;
                }

                // The first read of a bound local carries its binding.
                _pending.Remove(node);
                string value;
                if (!Print(binding, out value, out why))
                    return false;
                text = "(" + local + " = " + value + ")";
                return true;
            }

            if (node.Name == "model" && node.Type == typeof(object))
            {
                text = "model";
                return true;
            }

            if (node.Name == "chained" && node.Type == typeof(object))
            {
                text = "chained";
                return true;
            }

            if (node.Name == "root" && node.Type == typeof(object))
            {
                text = "root";
                return true;
            }

            if (node.Name == "props" && node.Type == typeof(object[]))
            {
                text = "props";
                return true;
            }

            why = "unknown parameter '" + (node.Name ?? "?") + "'";
            return false;
        }

        private bool PrintUnary(UnaryExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.TypeAs)
            {
                string operand;
                string operandWhy;
                if (!Print(node.Operand, out operand, out operandWhy))
                {
                    why = operandWhy;
                    return false;
                }

                string target;
                string targetWhy;
                if (!TypeNamePrinter.TrySpell(node.Type, out target, out targetWhy))
                {
                    why = "conversion target: " + targetWhy;
                    return false;
                }

                // Every operand the engine promotes is already converted in the tree; the cast
                // only names the recorded CLR type, so C# promotion never re-decides.
                text = node.NodeType == ExpressionType.TypeAs
                    ? Operand(node.Operand, operand) + " as " + target
                    : "(" + target + ")" + Operand(node.Operand, operand);
                return true;
            }

            string side;
            string sideWhy;
            if (!Print(node.Operand, out side, out sideWhy))
            {
                why = sideWhy;
                return false;
            }

            switch (node.NodeType)
            {
                case ExpressionType.Not:
                    text = "!" + Operand(node.Operand, side);
                    return true;
                case ExpressionType.Negate:
                    text = "-" + Operand(node.Operand, side);
                    return true;
                case ExpressionType.UnaryPlus:
                    text = "+" + Operand(node.Operand, side);
                    return true;
                case ExpressionType.OnesComplement:
                    text = "~" + Operand(node.Operand, side);
                    return true;
            }

            why = "unsupported unary '" + node.NodeType + "'";
            return false;
        }

        private bool PrintBinary(BinaryExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            if (node.NodeType == ExpressionType.ArrayIndex)
            {
                string array;
                string arrayWhy;
                if (!Print(node.Left, out array, out arrayWhy))
                {
                    why = arrayWhy;
                    return false;
                }

                string index;
                string indexWhy;
                if (!Print(node.Right, out index, out indexWhy))
                {
                    why = indexWhy;
                    return false;
                }

                text = Operand(node.Left, array) + "[" + index + "]";
                return true;
            }

            if (DividesByConstantZero(node))
            {
                why = "division by a constant zero";
                return false;
            }

            string left;
            string leftWhy;
            if (!Print(node.Left, out left, out leftWhy))
            {
                why = leftWhy;
                return false;
            }

            string right;
            string rightWhy;
            if (!Print(node.Right, out right, out rightWhy))
            {
                why = rightWhy;
                return false;
            }

            // String concatenation prints string.Concat as the engine's choice of overload;
            // every other operator method in the tree is a user-defined op over operands the
            // engine already converted, so the infix form applies it identically.
            if (node.Method != null && IsStringConcat(node.Method))
            {
                text = "string.Concat(" + left + ", " + right + ")";
                return true;
            }

            string op;
            if (!TryInfix(node.NodeType, out op))
            {
                why = "unsupported binary '" + node.NodeType + "'";
                return false;
            }

            string leftOperand = Operand(node.Left, left);
            string rightOperand = Operand(node.Right, right);
            if (node.Method == null)
            {
                // No operator method on an equality over references is reference equality. A bare infix
                // would let C# bind an operator the operand type declares or inherits.
                if ((node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual) &&
                    !node.Left.Type.IsValueType && !node.Right.Type.IsValueType)
                {
                    leftOperand = "(object)" + leftOperand;
                    rightOperand = "(object)" + rightOperand;
                }
            }
            else if (!node.IsLifted)
            {
                // The operator the engine bound: operands typed exactly as its parameters leave C# overload
                // resolution no better candidate.
                var parameters = node.Method.GetParameters();
                if (parameters.Length == 2)
                {
                    if (!TryPinOperand(node.Left, parameters[0].ParameterType, ref leftOperand, out why) ||
                        !TryPinOperand(node.Right, parameters[1].ParameterType, ref rightOperand, out why))
                        return false;
                }
            }

            text = leftOperand + " " + op + " " + rightOperand;
            return true;
        }

        private static bool TryPinOperand(Expression operand, Type parameterType, ref string text, out string why)
        {
            why = null;
            if (operand.Type == parameterType)
                return true;
            string spelling;
            string spellWhy;
            if (!TypeNamePrinter.TrySpell(parameterType, out spelling, out spellWhy))
            {
                why = "operator parameter type: " + spellWhy;
                return false;
            }

            text = "(" + spelling + ")" + text;
            return true;
        }

        private static bool IsStringConcat(MethodInfo method) =>
            method != null && method.DeclaringType == typeof(string) &&
            string.Equals(method.Name, nameof(string.Concat), StringComparison.Ordinal);

        private static bool TryInfix(ExpressionType type, out string op)
        {
            op = null;
            switch (type)
            {
                case ExpressionType.Add:
                    op = "+";
                    return true;
                case ExpressionType.Subtract:
                    op = "-";
                    return true;
                case ExpressionType.Multiply:
                    op = "*";
                    return true;
                case ExpressionType.Divide:
                    op = "/";
                    return true;
                case ExpressionType.Modulo:
                    op = "%";
                    return true;
                case ExpressionType.LeftShift:
                    op = "<<";
                    return true;
                case ExpressionType.RightShift:
                    op = ">>";
                    return true;
                case ExpressionType.LessThan:
                    op = "<";
                    return true;
                case ExpressionType.LessThanOrEqual:
                    op = "<=";
                    return true;
                case ExpressionType.GreaterThan:
                    op = ">";
                    return true;
                case ExpressionType.GreaterThanOrEqual:
                    op = ">=";
                    return true;
                case ExpressionType.Equal:
                    op = "==";
                    return true;
                case ExpressionType.NotEqual:
                    op = "!=";
                    return true;
                case ExpressionType.And:
                    op = "&";
                    return true;
                case ExpressionType.ExclusiveOr:
                    op = "^";
                    return true;
                case ExpressionType.Or:
                    op = "|";
                    return true;
                case ExpressionType.AndAlso:
                    op = "&&";
                    return true;
                case ExpressionType.OrElse:
                    op = "||";
                    return true;
                case ExpressionType.Coalesce:
                    op = "??";
                    return true;
            }

            return false;
        }

        private bool PrintMethodCall(MethodCallExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            var method = node.Method;
            if (method == null || node.Object != null)
            {
                why = "instance call";
                return false;
            }

            // Generated code names only what the consumer's assembly can see: a call the
            // engine bound to an internal target — the default built-ins included — is declined and
            // rebuilt from data at load, listed in the template's HED7031 notice.
            if (!method.IsPublic || !method.IsStatic)
            {
                why = "call to '" + (node.Method != null ? node.Method.Name : "?") +
                    "' is not a public static method";
                return false;
            }

            if (!method.IsPublic || !method.IsStatic)
            {
                why = "call to '" + method.Name + "' is not a public static method";
                return false;
            }

            if (method.IsGenericMethodDefinition)
            {
                why = "open generic call to '" + method.Name + "'";
                return false;
            }

            string declaring;
            string declaringWhy;
            if (!TypeNamePrinter.TrySpell(method.DeclaringType, out declaring, out declaringWhy))
            {
                why = "call declaring type: " + declaringWhy;
                return false;
            }

            var arguments = new string[node.Arguments.Count];
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument;
                string argumentWhy;
                if (!Print(node.Arguments[i], out argument, out argumentWhy))
                {
                    why = argumentWhy;
                    return false;
                }

                arguments[i] = argument;
            }

            string generic = string.Empty;
            if (method.IsGenericMethod)
            {
                var parameters = method.GetGenericArguments();
                var spelled = new string[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    string parameter;
                    string parameterWhy;
                    if (!TypeNamePrinter.TrySpell(parameters[i], out parameter, out parameterWhy))
                    {
                        why = "call type argument: " + parameterWhy;
                        return false;
                    }

                    spelled[i] = parameter;
                }

                generic = "<" + string.Join(", ", spelled) + ">";
            }

            text = declaring + "." + Heddle.Helpers.TypeNameHelper.CreateEscapedIdentifier(method.Name) + generic + "(" + string.Join(", ", arguments) + ")";
            return true;
        }

        private bool PrintMember(MemberExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            var property = node.Member as PropertyInfo;
            if (property == null)
            {
                why = "non-property member '" + (node.Member != null ? node.Member.Name : "?") + "'";
                return false;
            }

            if (!TypeNamePrinter.IsCallableFromConsumer(property))
            {
                why = "member '" + property.Name + "' is not a public instance property";
                return false;
            }

            if (TypeNamePrinter.IsObsoleteError(property))
            {
                why = "member '" + property.Name + "' is [Obsolete(error: true)]";
                return false;
            }

            string declaring;
            string declaringWhy;
            if (!TypeNamePrinter.TrySpell(property.DeclaringType, out declaring, out declaringWhy))
            {
                why = "member declaring type: " + declaringWhy;
                return false;
            }

            string receiver;
            string receiverWhy;
            if (node.Expression == null)
            {
                why = "static member '" + property.Name + "'";
                return false;
            }

            if (!Print(node.Expression, out receiver, out receiverWhy))
            {
                why = receiverWhy;
                return false;
            }

            if (property.GetIndexParameters().Length != 0)
            {
                why = "indexer '" + property.Name + "' as plain member";
                return false;
            }

            text = Operand(node.Expression, receiver) + "." + Heddle.Helpers.TypeNameHelper.CreateEscapedIdentifier(property.Name);
            return true;
        }

        private bool PrintIndex(IndexExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            var indexer = node.Indexer;
            if (indexer == null)
            {
                why = "indexer without property";
                return false;
            }

            if (!TypeNamePrinter.IsCallableFromConsumer(indexer))
            {
                why = "indexer '" + indexer.Name + "' is not a public instance property";
                return false;
            }

            if (TypeNamePrinter.IsObsoleteError(indexer))
            {
                why = "indexer '" + indexer.Name + "' is [Obsolete(error: true)]";
                return false;
            }

            string declaring;
            string declaringWhy;
            if (!TypeNamePrinter.TrySpell(indexer.DeclaringType, out declaring, out declaringWhy))
            {
                why = "indexer declaring type: " + declaringWhy;
                return false;
            }

            string receiver;
            string receiverWhy;
            if (node.Object == null)
            {
                why = "static indexer '" + indexer.Name + "'";
                return false;
            }

            if (!Print(node.Object, out receiver, out receiverWhy))
            {
                why = receiverWhy;
                return false;
            }

            var arguments = new string[node.Arguments.Count];
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument;
                string argumentWhy;
                if (!Print(node.Arguments[i], out argument, out argumentWhy))
                {
                    why = argumentWhy;
                    return false;
                }

                arguments[i] = argument;
            }

            // The indexer the engine bound, with the converted arguments it built.
            text = Operand(node.Object, receiver) + "[" + string.Join(", ", arguments) + "]";
            return true;
        }

        private bool PrintConditional(ConditionalExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            string test;
            string testWhy;
            if (!Print(node.Test, out test, out testWhy))
            {
                why = testWhy;
                return false;
            }

            string whenTrue;
            string trueWhy;
            if (!Print(node.IfTrue, out whenTrue, out trueWhy))
            {
                why = trueWhy;
                return false;
            }

            string whenFalse;
            string falseWhy;
            if (!Print(node.IfFalse, out whenFalse, out falseWhy))
            {
                why = falseWhy;
                return false;
            }

            // The engine's common type is already unified in the tree (explicit Convert arms),
            // so the ternary only names the recorded shapes.
            text = Operand(node.Test, test) + " ? " + Operand(node.IfTrue, whenTrue) + " : " +
                Operand(node.IfFalse, whenFalse);
            return true;
        }

        /// <summary>Prints a block of receiver bindings as a <b>single expression</b>: each binding is folded
        /// into the first read of its local — <c>(h = receiver) == default(T) ? default(R) : h.Member</c> — so
        /// the receiver is read exactly where the tree reads it. Lifting the bindings into statements ahead of
        /// the <c>return</c> reads them unconditionally and first, which runs the untaken arm of every
        /// <c>?:</c>, <c>&amp;&amp;</c>, <c>||</c> and <c>??</c> above the block and moves them ahead of operands
        /// written to their left. Only a local's declaration, which evaluates nothing, is a statement.
        /// <para>The fold is exact only when the first thing each consumer evaluates is the local bound just
        /// before it — true of every null-safe hop the engine builds, whose consumer opens with the null test.
        /// Any other block is declined.</para></summary>
        private bool PrintBlock(BlockExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            int last = node.Expressions.Count - 1;
            if (last < 1)
            {
                why = "unsupported block shape";
                return false;
            }

            var bound = new ParameterExpression[last];
            for (int i = 0; i < last; i++)
            {
                var bind = node.Expressions[i] as BinaryExpression;
                bound[i] = bind != null && bind.NodeType == ExpressionType.Assign
                    ? bind.Left as ParameterExpression
                    : null;
                var consumer = i + 1 < last ? ((BinaryExpression)node.Expressions[i + 1]).Right : node.Expressions[last];
                if (bound[i] == null || !node.Variables.Contains(bound[i]) || _pending.ContainsKey(bound[i]) ||
                    FirstEvaluated(consumer) != bound[i])
                {
                    why = "unsupported block shape";
                    return false;
                }
            }

            foreach (var variable in node.Variables)
            {
                if (_locals.ContainsKey(variable))
                    continue;
                string spelling;
                string spellWhy;
                if (!TypeNamePrinter.TrySpell(variable.Type, out spelling, out spellWhy))
                {
                    why = "block local type: " + spellWhy;
                    return false;
                }

                string name = "h_" + _localCount;
                _localCount++;
                _locals.Add(variable, name);
                _statements.Add(spelling + " " + name + " = default(" + spelling + ");");
            }

            for (int i = 0; i < last; i++)
                _pending.Add(bound[i], ((BinaryExpression)node.Expressions[i]).Right);
            if (!Print(node.Expressions[last], out text, out why))
                return false;
            for (int i = 0; i < last; i++)
            {
                if (_pending.ContainsKey(bound[i]))
                {
                    why = "unsupported block shape";
                    return false;
                }
            }

            return true;
        }

        /// <summary>The leaf an expression evaluates before anything else in it.</summary>
        private static Expression FirstEvaluated(Expression node)
        {
            while (true)
            {
                var conditional = node as ConditionalExpression;
                var binary = node as BinaryExpression;
                var unary = node as UnaryExpression;
                var member = node as MemberExpression;
                var index = node as IndexExpression;
                if (conditional != null)
                    node = conditional.Test;
                else if (binary != null && binary.NodeType != ExpressionType.Assign)
                    node = binary.Left;
                else if (unary != null)
                    node = unary.Operand;
                else if (member != null && member.Expression != null)
                    node = member.Expression;
                else if (index != null && index.Object != null)
                    node = index.Object;
                else
                    return node;
            }
        }

        private bool PrintNewArray(NewArrayExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            if (node.NodeType != ExpressionType.NewArrayInit)
            {
                why = "unsupported array '" + node.NodeType + "'";
                return false;
            }

            string element;
            string elementWhy;
            if (!TypeNamePrinter.TrySpell(node.Type.GetElementType(), out element, out elementWhy))
            {
                why = "array element type: " + elementWhy;
                return false;
            }

            var items = new string[node.Expressions.Count];
            for (int i = 0; i < items.Length; i++)
            {
                string item;
                string itemWhy;
                if (!Print(node.Expressions[i], out item, out itemWhy))
                {
                    why = itemWhy;
                    return false;
                }

                items[i] = item;
            }

            // Params expansion spelled out, exactly as the engine's BuildCallArguments built it.
            text = "new " + element + "[] { " + string.Join(", ", items) + " }";
            return true;
        }

        private bool PrintDefault(DefaultExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            string spelling;
            string spellWhy;
            if (!TypeNamePrinter.TrySpell(node.Type, out spelling, out spellWhy))
            {
                why = "default type: " + spellWhy;
                return false;
            }

            text = "default(" + spelling + ")";
            return true;
        }

        /// <summary>Whether <paramref name="node"/> printed as a C# primary expression, which binds tighter
        /// than every operator and so stands as an operand without grouping. Decided from the node kind:
        /// the printed text cannot say, because <c>(A) + (B)</c> opens and closes with a parenthesis and is
        /// not grouped at all.</summary>
        private static bool IsPrimary(Expression node, string text)
        {
            // A folded constant subtree printed as a literal, whatever node kind it was.
            if (!(node is DefaultExpression) && IsConstantSubtree(node))
                return text.Length != 0 && text[0] != '(' && text[0] != '-';
            switch (node.NodeType)
            {
                case ExpressionType.Parameter:
                case ExpressionType.MemberAccess:
                case ExpressionType.Index:
                case ExpressionType.ArrayIndex:
                case ExpressionType.Call:
                case ExpressionType.Default:
                    return true;
                case ExpressionType.Constant:
                    // A cast-spelled literal (small integrals, enums) and a signed literal are unary
                    // expressions, not primaries.
                    return text.Length != 0 && text[0] != '(' && text[0] != '-';
                case ExpressionType.Add:
                    // Printed as a string.Concat call.
                    return IsStringConcat(((BinaryExpression)node).Method);
            }

            return false;
        }

        private static string Operand(Expression node, string text) =>
            IsPrimary(node, text) ? text : "(" + text + ")";
    }
}
