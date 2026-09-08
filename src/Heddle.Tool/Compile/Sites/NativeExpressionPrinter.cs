using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Heddle.Tool.Compile.Sites
{
    /// <summary>Prints one native-expression site (P3-R4) from the typed bound tree the engine
    /// compiled: every operand cast to its promoted CLR type is already a <c>Convert</c> in the
    /// tree, so C#'s own promotion never decides; the chosen overload is the <c>MethodCall</c>
    /// method; hop forms are the <c>Condition</c> shapes. A bound node kind with no arm here is
    /// declined, never guessed.</summary>
    internal sealed class NativeExpressionPrinter
    {
        private readonly List<string> _statements = new List<string>();
        private readonly Dictionary<ParameterExpression, string> _locals =
            new Dictionary<ParameterExpression, string>();
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
            sb.Append("            return ").Append(resultCast).Append(";\n");
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
            if (value is string text)
            {
                literal = Quote(text);
                return true;
            }

            if (value is bool boolean)
            {
                literal = boolean ? "true" : "false";
                return true;
            }

            if (value is char character)
            {
                literal = "'" + EscapeChar(character) + "'";
                return true;
            }

            if (value is decimal number)
            {
                literal = number.ToString(CultureInfo.InvariantCulture) + "m";
                return true;
            }

            if (value is double)
            {
                double dbl = (double)value;
                if (double.IsNaN(dbl) || double.IsInfinity(dbl))
                {
                    why = "non-finite double constant";
                    return false;
                }

                literal = dbl.ToString("R", CultureInfo.InvariantCulture) + "d";
                return true;
            }

            if (value is float)
            {
                float flt = (float)value;
                if (float.IsNaN(flt) || float.IsInfinity(flt))
                {
                    why = "non-finite float constant";
                    return false;
                }

                literal = flt.ToString("R", CultureInfo.InvariantCulture) + "f";
                return true;
            }

            if (value is int)
            {
                literal = ((int)value).ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (value is uint)
            {
                literal = ((uint)value).ToString(CultureInfo.InvariantCulture) + "u";
                return true;
            }

            if (value is long)
            {
                literal = ((long)value).ToString(CultureInfo.InvariantCulture) + "L";
                return true;
            }

            if (value is ulong)
            {
                literal = ((ulong)value).ToString(CultureInfo.InvariantCulture) + "UL";
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

                literal = "(" + enumSpelling + ")" + numericText;
                return true;
            }

            why = "constant of type '" + (type.FullName ?? type.Name) + "'";
            return false;
        }

        private static string Quote(string text)
        {
            var sb = new StringBuilder(text.Length + 2);
            sb.Append('"');
            foreach (var character in text)
                sb.Append(EscapeChar(character));
            sb.Append('"');
            return sb.ToString();
        }

        private static string EscapeChar(char character)
        {
            switch (character)
            {
                case '"': return "\\\"";
                case '\\': return "\\\\";
                case '\0': return "\\0";
                case '\a': return "\\a";
                case '\b': return "\\b";
                case '\f': return "\\f";
                case '\n': return "\\n";
                case '\r': return "\\r";
                case '\t': return "\\t";
                case '\v': return "\\v";
                default:
                    if (char.IsControl(character))
                        return "\\u" + ((int)character).ToString("X4");
                    return character.ToString();
            }
        }

        private bool PrintParameter(ParameterExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            string local;
            if (_locals.TryGetValue(node, out local))
            {
                text = local;
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
                    ? Paren(operand) + " as " + target
                    : "(" + target + ")" + Paren(operand);
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
                    text = "!" + Paren(side);
                    return true;
                case ExpressionType.Negate:
                    text = "-" + Paren(side);
                    return true;
                case ExpressionType.UnaryPlus:
                    text = "+" + Paren(side);
                    return true;
                case ExpressionType.OnesComplement:
                    text = "~" + Paren(side);
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

                text = Paren(array) + "[" + index + "]";
                return true;
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

            text = Paren(left) + " " + op + " " + Paren(right);
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

            // Generated code names only what the consumer's assembly can see (P3-R2): a call the
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

            text = declaring + "." + method.Name + generic + "(" + string.Join(", ", arguments) + ")";
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

            text = Paren(receiver) + "." + property.Name;
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
            text = Paren(receiver) + "[" + string.Join(", ", arguments) + "]";
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
            text = Paren(test) + " ? " + Paren(whenTrue) + " : " + Paren(whenFalse);
            return true;
        }

        private bool PrintBlock(BlockExpression node, out string text, out string why)
        {
            text = null;
            why = null;
            foreach (var variable in node.Variables)
            {
                if (!_locals.ContainsKey(variable))
                {
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
            }

            for (int i = 0; i < node.Expressions.Count; i++)
            {
                bool last = i == node.Expressions.Count - 1;
                string part;
                string partWhy;
                var assign = node.Expressions[i] as BinaryExpression;
                if (!last && assign != null && assign.NodeType == ExpressionType.Assign)
                {
                    var target = assign.Left as ParameterExpression;
                    if (target == null)
                    {
                        why = "assign to non-local";
                        return false;
                    }

                    string name;
                    if (!_locals.TryGetValue(target, out name))
                    {
                        why = "assign to undeclared local";
                        return false;
                    }

                    string value;
                    string valueWhy;
                    if (!Print(assign.Right, out value, out valueWhy))
                    {
                        why = valueWhy;
                        return false;
                    }

                    _statements.Add(name + " = " + value + ";");
                    continue;
                }

                if (!Print(node.Expressions[i], out part, out partWhy))
                {
                    why = partWhy;
                    return false;
                }

                if (!last)
                {
                    why = "non-assign block statement";
                    return false;
                }

                text = part;
            }

            if (text == null)
            {
                why = "empty block";
                return false;
            }

            return true;
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

        private static bool IsAtomic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            char first = text[0];
            char last = text[text.Length - 1];
            if (first == '(' && last == ')')
                return true;
            foreach (var character in text)
            {
                if (character == ' ' || character == '?' || character == ':' || character == '+' ||
                    character == '-' || character == '*' || character == '/' || character == '%' ||
                    character == '<' || character == '>' || character == '=' || character == '!' ||
                    character == '&' || character == '|' || character == '^')
                    return false;
            }

            return true;
        }

        private static string Paren(string text) => IsAtomic(text) ? text : "(" + text + ")";
    }
}
