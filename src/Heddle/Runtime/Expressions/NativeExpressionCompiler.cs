using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Heddle.Data;
using Heddle.Language;
using Heddle.Language.Expressions;
using Heddle.Runtime.Parameters;
using Heddle.Strings.Core;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// Compiles a native-expression AST to a runtime parameter (the existing
    /// <see cref="CompiledParameter"/>/<see cref="ConstantParameter"/> shapes) using
    /// <see cref="System.Linq.Expressions"/>. Records positioned compile errors on failure; never throws for
    /// template input.
    /// </summary>
    internal sealed class NativeExpressionCompiler
    {
        private static readonly MethodInfo ObjectEquals =
            typeof(object).GetMethod(nameof(object.Equals), new[] { typeof(object), typeof(object) });

        private static readonly MethodInfo ConcatStringString =
            typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) });

        private static readonly MethodInfo ConcatObjectObject =
            typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(object), typeof(object) });

        private static readonly MethodInfo FormatCompositeMethod =
            typeof(BuiltInFunctions).GetMethod("Format", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(object[]) }, null);

        private static readonly MethodInfo RangeThreeArgMethod =
            typeof(BuiltInFunctions).GetMethod("Range", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(int), typeof(int), typeof(int) }, null);

        private readonly CompileScope _compileScope;
        private readonly ParseContext _parseContext;
        private readonly FunctionRegistry _registry;
        private readonly ParameterExpression _model = Expression.Parameter(typeof(object), "model");
        private readonly ParameterExpression _chained = Expression.Parameter(typeof(object), "chained");
        private readonly ParameterExpression _root = Expression.Parameter(typeof(object), "root");
        private readonly ParameterExpression _props = Expression.Parameter(typeof(object[]), "props");
        private bool _failed;
        private bool _foldable = true;
        private bool _usesProps;

        private NativeExpressionCompiler(CompileScope compileScope, ParseContext parseContext)
        {
            _compileScope = compileScope;
            _parseContext = parseContext;
            _registry = compileScope.Options.Functions ?? FunctionRegistry.Default;
            _registry.Freeze();
        }

        internal static IRuntimeParameter Compile(ExprNode expression, CompileScope compileScope,
            ParseContext parseContext, out ExType resultType)
        {
            // 'this' as a whole expression is the model passthrough — compiles to the existing EmptyParameter so
            // it works on dynamic scopes too, exactly like the empty member path (D5).
            if (expression is ThisNode)
            {
                resultType = compileScope.ScopeType;
                return new EmptyParameter();
            }

            var compiler = new NativeExpressionCompiler(compileScope, parseContext);
            var body = compiler.Visit(expression);
            if (compiler._failed || body == null)
            {
                resultType = typeof(object);
                return null;
            }

            resultType = new ExType(body.Type);
            var boxed = Expression.Convert(body, typeof(object));

            if (compiler._foldable && !compiler._usesProps)
            {
                try
                {
                    var value = Expression.Lambda<Func<object>>(boxed).Compile()();
                    return new ConstantParameter(value);
                }
                catch (Exception)
                {
                    // Fall through to a compiled parameter if folding blows up unexpectedly.
                }
            }

            if (compiler._usesProps)
            {
                // Props-aware delegate shape (D9): a fourth object[] parameter bound to scope.PropsData. Emitted
                // only when the tree contains a prop root; prop-free expressions keep today's 3-arg shape.
                var propsLambda = Expression.Lambda<Func<object, object, object, object[], object>>(
                    boxed, compiler._model, compiler._chained, compiler._root, compiler._props);
                return new PropsCompiledParameter { ParameterImplementation = propsLambda.Compile() };
            }

            var lambda = Expression.Lambda<Func<object, object, object, object>>(
                boxed, compiler._model, compiler._chained, compiler._root);
            return new CompiledParameter { ParameterImplementation = lambda.Compile() };
        }

        private Expression Visit(ExprNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return VisitLiteral(literal);
                case ThisNode thisNode:
                    return VisitThis(thisNode);
                case PathNode path:
                    return VisitPath(path);
                case IndexNode index:
                    return VisitIndex(index);
                case CallNode call:
                    return VisitCall(call);
                case MethodCallNode method:
                    return Fail(method.Position, HeddleDiagnosticIds.MethodCallNotAvailable,
                        "Method calls are not available in native expressions — register a function with TemplateOptions.Functions or use the @ C# tier.");
                case UnaryNode unary:
                    return VisitUnary(unary);
                case BinaryNode binary:
                    return VisitBinary(binary);
                case TernaryNode ternary:
                    return VisitTernary(ternary);
            }

            return null;
        }

        #region Literals & paths

        private Expression VisitLiteral(LiteralNode literal)
        {
            if (literal.LiteralError != null)
                return Fail(literal.Position, HeddleDiagnosticIds.BinaryOperatorNotDefined, literal.LiteralError);
            if (literal.Value == null)
                return Expression.Constant(null, typeof(object));
            return Expression.Constant(literal.Value, literal.Value.GetType());
        }

        private Expression VisitThis(ThisNode node)
        {
            // As an operand or path root, 'this' is a typed operand following the phase 1 dynamic-operand rule.
            _foldable = false;
            var scopeType = _compileScope.ScopeType;
            if (scopeType.IsDynamic)
                return FailTypedModel(node.Position);
            return Expression.Convert(_model, scopeType.Type);
        }

        private Expression VisitPath(PathNode path)
        {
            _foldable = false;

            Expression input;
            ExType startType;
            if (path.Target != null)
            {
                var target = Visit(path.Target);
                if (target == null)
                    return null;
                startType = new ExType(target.Type);
                input = Expression.Convert(target, typeof(object));
            }
            else if (path.RootRef)
            {
                startType = _compileScope.RootScopeType;
                input = _root;
            }
            else
            {
                // Phase 5 (D9): a body prop read wins on the first segment (never for :: root refs, handled above).
                var propExpr = TryVisitPropRoot(path);
                if (propExpr != null)
                    return propExpr;
                startType = _compileScope.ScopeType;
                input = _model;
            }

            if (startType.IsDynamic)
                return FailTypedModel(path.Position);

            var resolution = MemberPathResolver.TryResolve(startType, path.Segments.ToArray());
            if (resolution.Kind == MemberPathResolutionKind.Failed)
                return Fail(path.Position, HeddleDiagnosticIds.PropertyNotFound, resolution.FailureMessage);
            if (resolution.Kind == MemberPathResolutionKind.DynamicHop)
                return FailTypedModel(path.Position);

            return ModelParameter.BuildNullSafePropertyChain(input, resolution.Properties);
        }

        private Expression FailTypedModel(BlockPosition position)
        {
            return Fail(position, HeddleDiagnosticIds.TypedModelRequired,
                "Native expressions require a typed model; declare @model(...) / ':: <Type>' or use the @ C# tier.");
        }

        /// <summary>
        /// Native-tier body prop read (D9): when the path's first segment names a prop in the active layout,
        /// roots the read at <c>Convert(props[index], propType)</c> and hops the remaining segments; sets the
        /// props-aware delegate flag. Returns <c>null</c> when the segment is not a prop (ordinary model root).
        /// </summary>
        private Expression TryVisitPropRoot(PathNode path)
        {
            var layout = _compileScope.CompileContext.ActivePropLayout;
            if (layout == null || path.Segments.Count == 0 || !layout.TryGet(path.Segments[0], out var slot))
                return null;

            PropLayout.WarnIfShadowsMember(_compileScope, _compileScope.ScopeType, slot.Name, path.Position);
            _usesProps = true;

            Expression propRoot = Expression.Convert(
                Expression.ArrayIndex(_props, Expression.Constant(slot.Index)), slot.Type.Type);
            if (path.Segments.Count == 1)
                return propRoot;

            var rest = new string[path.Segments.Count - 1];
            for (int i = 1; i < path.Segments.Count; i++)
                rest[i - 1] = path.Segments[i];
            var resolution = MemberPathResolver.TryResolve(slot.Type, rest);
            if (resolution.Kind == MemberPathResolutionKind.Failed)
                return Fail(path.Position, HeddleDiagnosticIds.PropertyNotFound, resolution.FailureMessage);
            if (resolution.Kind == MemberPathResolutionKind.DynamicHop)
                return FailTypedModel(path.Position);

            var boxedRoot = Expression.Convert(propRoot, typeof(object));
            return ModelParameter.BuildNullSafePropertyChain(boxedRoot, resolution.Properties);
        }

        private Expression VisitIndex(IndexNode index)
        {
            _foldable = false;
            var target = Visit(index.Target);
            if (target == null)
                return null;
            var args = new Expression[index.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = Visit(index.Arguments[i]);
                if (args[i] == null)
                    return null;
            }

            var targetType = target.Type;
            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                var indices = new Expression[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var argUnderlying = Nullable.GetUnderlyingType(args[i].Type) ?? args[i].Type;
                    if (!NumericPromotion.IsIntegral(argUnderlying))
                        return FailIndexer(index, targetType, args);
                    indices[i] = args[i].Type == typeof(int) ? args[i] : Expression.Convert(args[i], typeof(int));
                }

                Expression access = args.Length == 1
                    ? Expression.ArrayIndex(target, indices[0])
                    : Expression.ArrayIndex(target, indices);
                return NullSafeTarget(target, access, elementType);
            }

            var indexer = FindIndexer(targetType, args);
            if (indexer == null)
                return FailIndexer(index, targetType, args);
            var indexParams = indexer.GetIndexParameters();
            var converted = new Expression[args.Length];
            for (int i = 0; i < args.Length; i++)
                converted[i] = ConvertTo(args[i], indexParams[i].ParameterType);
            Expression indexerAccess = Expression.Property(target, indexer, converted);
            return NullSafeTarget(target, indexerAccess, indexer.PropertyType);
        }

        private static Expression NullSafeTarget(Expression target, Expression access, Type resultType)
        {
            if (target.Type.IsValueType)
                return access;
            return Expression.Condition(
                Expression.Equal(target, Expression.Constant(null, target.Type)),
                Expression.Default(resultType), access);
        }

        private static PropertyInfo FindIndexer(Type type, Expression[] args)
        {
            foreach (var property in type.GetProperties(MemberPathResolver.MemberBindingFlags))
            {
                var indexParams = property.GetIndexParameters();
                if (indexParams.Length != args.Length)
                    continue;
                if (!MemberPathResolver.IsAccessible(property))
                    continue;
                bool matches = true;
                for (int i = 0; i < args.Length; i++)
                {
                    if (!IsConvertibleTo(args[i], indexParams[i].ParameterType))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return property;
            }

            return null;
        }

        private Expression FailIndexer(IndexNode index, Type targetType, Expression[] args)
        {
            var argTypes = string.Join(", ", args.Select(a => FriendlyName(a.Type)));
            return Fail(index.Position, HeddleDiagnosticIds.IndexerNotFound,
                $"Type {FriendlyName(targetType)} has no accessible indexer that takes ({argTypes}).");
        }

        #endregion

        #region Function calls

        private Expression VisitCall(CallNode call)
        {
            _foldable = false;
            var overloads = _registry.GetOverloads(call.Name);
            if (overloads.Count == 0)
            {
                if (TemplateFactory.Exists(call.Name) || _parseContext.DefenitionExists(call.Name))
                    return Fail(call.Position, HeddleDiagnosticIds.ExtensionCalledAsFunction,
                        $"'{call.Name}' is an extension, not a registered function — extensions cannot be called inside a native expression. Use a call chain, or register a function with TemplateOptions.Functions.");
                return Fail(call.Position, HeddleDiagnosticIds.UnknownFunction,
                    $"Cannot find extension or registered function '{call.Name}'. Register it with TemplateOptions.Functions, or check the name.");
            }

            var argExprs = new Expression[call.Arguments.Count];
            for (int i = 0; i < argExprs.Length; i++)
            {
                argExprs[i] = Visit(call.Arguments[i]);
                if (argExprs[i] == null)
                    return null;
            }

            var bind = BindOverload(overloads, argExprs, out var chosen, out var expanded);
            if (bind == BindOutcome.Ambiguous)
            {
                var candidates = string.Join(", ", overloads.Select(o => o.ToSignatureString()));
                return Fail(call.Position, HeddleDiagnosticIds.AmbiguousFunctionCall,
                    $"The call to function '{call.Name}' is ambiguous between: {candidates}.");
            }

            if (bind == BindOutcome.None)
            {
                var argTypes = string.Join(", ", argExprs.Select(a => FriendlyName(a.Type)));
                var candidates = string.Join(", ", overloads.Select(o => o.ToSignatureString()));
                return Fail(call.Position, HeddleDiagnosticIds.NoFunctionOverload,
                    $"No overload of function '{call.Name}' takes ({argTypes}). Candidates: {candidates}.");
            }

            if (IsCompositeFormat(chosen))
            {
                var formatError = CheckCompositeFormat(call);
                if (formatError != null)
                    return formatError;
            }

            // HED4001 (phase 4 D3): the built-in three-argument range with a statically-visible non-positive
            // literal (or sign-prefixed literal) step is a compile error, positioned at the step argument.
            // Scoped to the built-in MethodInfo by reference — a host-replaced 'range' governs its own step
            // rules. More complex constant shapes fall through to the render-time guard (phase 1 D17).
            if (chosen.Method != null && chosen.Method == RangeThreeArgMethod && call.Arguments.Count == 3 &&
                TryGetLiteralIntStep(call.Arguments[2], out var step) && step <= 0)
            {
                return Fail(call.Arguments[2].Position, HeddleDiagnosticIds.RangeStepNotPositive,
                    string.Format(CultureInfo.InvariantCulture, BuiltInFunctions.RangeStepMessageFormat, step));
            }

            var finalArgs = BuildCallArguments(chosen, argExprs, expanded);
            if (chosen.Method != null)
                return Expression.Call(null, chosen.Method, finalArgs);
            return Expression.Invoke(Expression.Constant(chosen.Target, chosen.Target.GetType()), finalArgs);
        }

        private enum BindOutcome { Bound, None, Ambiguous }

        private static BindOutcome BindOverload(IReadOnlyList<FunctionEntry> overloads, Expression[] args,
            out FunctionEntry chosen, out bool expanded)
        {
            var outcome = BindTier(overloads, args, false, out chosen);
            expanded = false;
            if (outcome != BindOutcome.None)
                return outcome;
            expanded = true;
            return BindTier(overloads, args, true, out chosen);
        }

        private static BindOutcome BindTier(IReadOnlyList<FunctionEntry> overloads, Expression[] args, bool expanded,
            out FunctionEntry chosen)
        {
            chosen = null;
            var candidates = new List<(FunctionEntry entry, int[] ranks)>();
            foreach (var entry in overloads)
            {
                if (TryRank(entry, args, expanded, out var ranks))
                    candidates.Add((entry, ranks));
            }

            if (candidates.Count == 0)
                return BindOutcome.None;

            var nonDominated = new List<(FunctionEntry entry, int[] ranks)>();
            foreach (var candidate in candidates)
            {
                bool dominated = candidates.Any(other => other.entry != candidate.entry && Dominates(other.ranks, candidate.ranks));
                if (!dominated)
                    nonDominated.Add(candidate);
            }

            if (nonDominated.Count == 1)
            {
                chosen = nonDominated[0].entry;
                return BindOutcome.Bound;
            }

            return BindOutcome.Ambiguous;
        }

        private static bool Dominates(int[] a, int[] b)
        {
            bool strictlyBetter = false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] > b[i])
                    return false;
                if (a[i] < b[i])
                    strictlyBetter = true;
            }

            return strictlyBetter;
        }

        private static bool TryRank(FunctionEntry entry, Expression[] args, bool expanded, out int[] ranks)
        {
            ranks = null;
            if (!expanded)
            {
                if (entry.ParameterTypes.Length != args.Length)
                    return false;
                var result = new int[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    int rank = ConversionRank(args[i], entry.ParameterTypes[i]);
                    if (rank < 0)
                        return false;
                    result[i] = rank;
                }

                ranks = result;
                return true;
            }

            if (!entry.HasParamsArray)
                return false;
            int fixedCount = entry.ParameterTypes.Length - 1;
            if (args.Length < fixedCount)
                return false;
            var elementType = entry.ParamsElementType;
            var vector = new int[args.Length];
            for (int i = 0; i < fixedCount; i++)
            {
                int rank = ConversionRank(args[i], entry.ParameterTypes[i]);
                if (rank < 0)
                    return false;
                vector[i] = rank;
            }

            for (int i = fixedCount; i < args.Length; i++)
            {
                int rank = ConversionRank(args[i], elementType);
                if (rank < 0)
                    return false;
                vector[i] = rank + 1; // expanded params ranked slightly worse than a fixed match
            }

            ranks = vector;
            return true;
        }

        /// <summary>Conversion rank: exact = 0, widening/reference/lifting = 1, boxing to object = 2; -1 = none.</summary>
        private static int ConversionRank(Expression arg, Type parameterType)
        {
            if (IsNullLiteral(arg))
            {
                if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null)
                    return 1;
                return -1;
            }

            var argType = arg.Type;
            if (argType == parameterType)
                return 0;
            if (parameterType == typeof(object))
                return 2;
            if (NumericPromotion.IsImplicitNumeric(argType, parameterType))
                return 1;
            if (!argType.IsValueType && parameterType.IsAssignableFrom(argType))
                return 1;
            if (argType.IsValueType && Nullable.GetUnderlyingType(parameterType) == argType)
                return 1;
            var argUnderlying = Nullable.GetUnderlyingType(argType);
            var paramUnderlying = Nullable.GetUnderlyingType(parameterType);
            if (argUnderlying != null && paramUnderlying != null &&
                (argUnderlying == paramUnderlying || NumericPromotion.IsImplicitNumeric(argUnderlying, paramUnderlying)))
                return 1;
            return -1;
        }

        private static bool IsConvertibleTo(Expression arg, Type parameterType)
        {
            return ConversionRank(arg, parameterType) >= 0;
        }

        private static Expression[] BuildCallArguments(FunctionEntry entry, Expression[] args, bool expanded)
        {
            if (!expanded)
            {
                var result = new Expression[args.Length];
                for (int i = 0; i < args.Length; i++)
                    result[i] = ConvertTo(args[i], entry.ParameterTypes[i]);
                return result;
            }

            int fixedCount = entry.ParameterTypes.Length - 1;
            var elementType = entry.ParamsElementType;
            var final = new Expression[entry.ParameterTypes.Length];
            for (int i = 0; i < fixedCount; i++)
                final[i] = ConvertTo(args[i], entry.ParameterTypes[i]);
            var elements = new Expression[args.Length - fixedCount];
            for (int i = fixedCount; i < args.Length; i++)
                elements[i - fixedCount] = ConvertTo(args[i], elementType);
            final[fixedCount] = Expression.NewArrayInit(elementType, elements);
            return final;
        }

        private static bool IsCompositeFormat(FunctionEntry entry)
        {
            return entry.Method != null && entry.Method == FormatCompositeMethod;
        }

        private Expression CheckCompositeFormat(CallNode call)
        {
            if (call.Arguments.Count == 0)
                return null;
            if (!(call.Arguments[0] is LiteralNode literal) || !(literal.Value is string format))
                return null;
            int valueArgs = call.Arguments.Count - 1;
            int maxIndex = MaxPlaceholderIndex(format);
            if (maxIndex >= valueArgs)
                return Fail(literal.Position, HeddleDiagnosticIds.FormatArgumentCountMismatch,
                    $"The format string references argument {{{maxIndex}}} but only {valueArgs} argument(s) were supplied.");
            return null;
        }

        private static int MaxPlaceholderIndex(string format)
        {
            int max = -1;
            int i = 0;
            while (i < format.Length)
            {
                char c = format[i];
                if (c == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{')
                    {
                        i += 2;
                        continue;
                    }

                    int j = i + 1;
                    int start = j;
                    while (j < format.Length && char.IsDigit(format[j]))
                        j++;
                    if (j > start)
                    {
                        int index = int.Parse(format.Substring(start, j - start));
                        if (index > max)
                            max = index;
                    }

                    i = j;
                    continue;
                }

                if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
                {
                    i += 2;
                    continue;
                }

                i++;
            }

            return max;
        }

        #endregion

        #region Unary

        private Expression VisitUnary(UnaryNode node)
        {
            var operand = Visit(node.Operand);
            if (operand == null)
                return null;
            var type = operand.Type;
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            bool lifted = underlying != type;

            switch (node.Operator)
            {
                case ExprOperator.Not:
                    if (underlying != typeof(bool))
                        return FailUnary(node, "!", type);
                    return Expression.Not(operand);

                case ExprOperator.Negate:
                {
                    if (underlying == typeof(ulong) || !NumericPromotion.IsNumeric(underlying))
                        return FailUnary(node, "-", type);
                    var promoted = underlying == typeof(uint) ? typeof(long) : NumericPromotion.UnaryPromote(underlying);
                    var target = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                    return Expression.Negate(ConvertTo(operand, target));
                }

                case ExprOperator.UnaryPlus:
                {
                    if (!NumericPromotion.IsNumeric(underlying))
                        return FailUnary(node, "+", type);
                    var promoted = NumericPromotion.UnaryPromote(underlying);
                    var target = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                    return Expression.UnaryPlus(ConvertTo(operand, target));
                }

                case ExprOperator.OnesComplement:
                {
                    if (underlying.IsEnum)
                    {
                        var enumUnderlying = Enum.GetUnderlyingType(underlying);
                        if (lifted)
                        {
                            var liftedUnderlying = typeof(Nullable<>).MakeGenericType(enumUnderlying);
                            var complemented = Expression.OnesComplement(Expression.Convert(operand, liftedUnderlying));
                            return Expression.Convert(complemented, type);
                        }

                        var result = Expression.OnesComplement(Expression.Convert(operand, enumUnderlying));
                        return Expression.Convert(result, underlying);
                    }

                    if (!NumericPromotion.IsIntegral(underlying))
                        return FailUnary(node, "~", type);
                    var promoted = NumericPromotion.UnaryPromote(underlying);
                    var target = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                    return Expression.OnesComplement(ConvertTo(operand, target));
                }
            }

            return null;
        }

        private Expression FailUnary(UnaryNode node, string op, Type type)
        {
            return Fail(node.Position, HeddleDiagnosticIds.UnaryOperatorNotDefined,
                $"Operator '{op}' is not defined for operand type {FriendlyName(type)}.");
        }

        #endregion

        #region Binary

        private Expression VisitBinary(BinaryNode node)
        {
            if (node.Operator == ExprOperator.AndAlso || node.Operator == ExprOperator.OrElse)
                return VisitLogical(node);
            if (node.Operator == ExprOperator.Coalesce)
                return VisitCoalesce(node);

            var left = Visit(node.Left);
            var right = Visit(node.Right);
            if (left == null || right == null)
                return null;

            switch (node.Operator)
            {
                case ExprOperator.Add:
                case ExprOperator.Subtract:
                case ExprOperator.Multiply:
                case ExprOperator.Divide:
                case ExprOperator.Modulo:
                    return VisitArithmetic(node, left, right);
                case ExprOperator.LeftShift:
                case ExprOperator.RightShift:
                    return VisitShift(node, left, right);
                case ExprOperator.LessThan:
                case ExprOperator.LessThanOrEqual:
                case ExprOperator.GreaterThan:
                case ExprOperator.GreaterThanOrEqual:
                    return VisitRelational(node, left, right);
                case ExprOperator.Equal:
                case ExprOperator.NotEqual:
                    return VisitEquality(node, left, right);
                case ExprOperator.And:
                case ExprOperator.ExclusiveOr:
                case ExprOperator.Or:
                    return VisitBitwise(node, left, right);
            }

            return null;
        }

        private Expression VisitArithmetic(BinaryNode node, Expression left, Expression right)
        {
            if (node.Operator == ExprOperator.Add &&
                (left.Type == typeof(string) || right.Type == typeof(string) || IsNullLiteral(left) || IsNullLiteral(right)))
            {
                if (left.Type == typeof(string) || right.Type == typeof(string))
                    return EmitStringConcat(left, right);
            }

            var leftU = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
            var rightU = Nullable.GetUnderlyingType(right.Type) ?? right.Type;
            bool lifted = left.Type != leftU || right.Type != rightU;

            if (NumericPromotion.IsNumeric(leftU) && NumericPromotion.IsNumeric(rightU))
            {
                if (!NumericPromotion.TryPromote(leftU, rightU, out var promoted))
                    return FailBinary(node, left.Type, right.Type);
                var target = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                var l = ConvertTo(left, target);
                var r = ConvertTo(right, target);
                return ArithmeticFactory(node.Operator, l, r);
            }

            try
            {
                return ArithmeticFactory(node.Operator, left, right);
            }
            catch (InvalidOperationException)
            {
                return FailBinary(node, left.Type, right.Type);
            }
        }

        private Expression EmitStringConcat(Expression left, Expression right)
        {
            if (left.Type == typeof(string) && right.Type == typeof(string))
                return Expression.Add(left, right, ConcatStringString);
            var l = Expression.Convert(left, typeof(object));
            var r = Expression.Convert(right, typeof(object));
            return Expression.Add(l, r, ConcatObjectObject);
        }

        private static Expression ArithmeticFactory(ExprOperator op, Expression l, Expression r)
        {
            switch (op)
            {
                case ExprOperator.Add: return Expression.Add(l, r);
                case ExprOperator.Subtract: return Expression.Subtract(l, r);
                case ExprOperator.Multiply: return Expression.Multiply(l, r);
                case ExprOperator.Divide: return Expression.Divide(l, r);
                default: return Expression.Modulo(l, r);
            }
        }

        private Expression VisitShift(BinaryNode node, Expression left, Expression right)
        {
            var leftU = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
            var rightU = Nullable.GetUnderlyingType(right.Type) ?? right.Type;
            bool lifted = left.Type != leftU || right.Type != rightU;

            if (leftU.IsEnum || !NumericPromotion.IsIntegral(leftU) || !NumericPromotion.IsIntegral(rightU))
                return FailBinary(node, left.Type, right.Type);

            Type shiftType;
            if (leftU == typeof(long) || leftU == typeof(ulong) || leftU == typeof(uint))
                shiftType = leftU;
            else
                shiftType = typeof(int);

            var leftTarget = lifted ? typeof(Nullable<>).MakeGenericType(shiftType) : shiftType;
            var rightTarget = lifted ? typeof(int?) : typeof(int);
            var l = ConvertTo(left, leftTarget);
            var r = ConvertTo(right, rightTarget);
            return node.Operator == ExprOperator.LeftShift ? Expression.LeftShift(l, r) : Expression.RightShift(l, r);
        }

        private Expression VisitRelational(BinaryNode node, Expression left, Expression right)
        {
            if (IsNullLiteral(left) || IsNullLiteral(right))
                return FailBinary(node, left.Type, right.Type);

            var leftU = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
            var rightU = Nullable.GetUnderlyingType(right.Type) ?? right.Type;
            bool lifted = left.Type != leftU || right.Type != rightU;

            if (NumericPromotion.IsNumeric(leftU) && NumericPromotion.IsNumeric(rightU))
            {
                if (!NumericPromotion.TryPromote(leftU, rightU, out var promoted))
                    return FailBinary(node, left.Type, right.Type);
                var target = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                return RelationalFactory(node.Operator, ConvertTo(left, target), ConvertTo(right, target));
            }

            try
            {
                return RelationalFactory(node.Operator, left, right);
            }
            catch (InvalidOperationException)
            {
                return FailBinary(node, left.Type, right.Type);
            }
        }

        private static Expression RelationalFactory(ExprOperator op, Expression l, Expression r)
        {
            switch (op)
            {
                case ExprOperator.LessThan: return Expression.LessThan(l, r, false, null);
                case ExprOperator.LessThanOrEqual: return Expression.LessThanOrEqual(l, r, false, null);
                case ExprOperator.GreaterThan: return Expression.GreaterThan(l, r, false, null);
                default: return Expression.GreaterThanOrEqual(l, r, false, null);
            }
        }

        private Expression VisitEquality(BinaryNode node, Expression left, Expression right)
        {
            bool op = node.Operator == ExprOperator.Equal;
            bool leftNull = IsNullLiteral(left);
            bool rightNull = IsNullLiteral(right);

            if (leftNull && rightNull)
                return Expression.Constant(op);

            if (leftNull || rightNull)
            {
                var other = leftNull ? right : left;
                if (!other.Type.IsValueType || Nullable.GetUnderlyingType(other.Type) != null)
                {
                    var typedNull = Expression.Constant(null, other.Type);
                    return op ? Expression.Equal(other, typedNull) : Expression.NotEqual(other, typedNull);
                }

                return FailBinary(node, left.Type, right.Type);
            }

            var leftU = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
            var rightU = Nullable.GetUnderlyingType(right.Type) ?? right.Type;
            bool lifted = left.Type != leftU || right.Type != rightU;

            if (NumericPromotion.IsNumeric(leftU) && NumericPromotion.IsNumeric(rightU))
            {
                if (!NumericPromotion.TryPromote(leftU, rightU, out var promoted))
                    return FailBinary(node, left.Type, right.Type);
                var target = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                var l = ConvertTo(left, target);
                var r = ConvertTo(right, target);
                return op ? Expression.Equal(l, r, false, null) : Expression.NotEqual(l, r, false, null);
            }

            try
            {
                return op ? Expression.Equal(left, right) : Expression.NotEqual(left, right);
            }
            catch (InvalidOperationException)
            {
                if (IsReferenceish(left.Type) && IsReferenceish(right.Type))
                {
                    var equals = Expression.Call(ObjectEquals,
                        Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
                    return op ? (Expression)equals : Expression.Not(equals);
                }

                return FailBinary(node, left.Type, right.Type);
            }
        }

        private Expression VisitBitwise(BinaryNode node, Expression left, Expression right)
        {
            var leftU = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
            var rightU = Nullable.GetUnderlyingType(right.Type) ?? right.Type;
            bool lifted = left.Type != leftU || right.Type != rightU;

            if (leftU == typeof(bool) && rightU == typeof(bool))
                return BitwiseFactory(node.Operator, left, right);

            if (leftU.IsEnum && rightU.IsEnum && leftU == rightU)
            {
                var enumUnderlying = Enum.GetUnderlyingType(leftU);
                var opType = lifted ? typeof(Nullable<>).MakeGenericType(enumUnderlying) : enumUnderlying;
                var resultType = lifted ? left.Type : leftU;
                var combined = BitwiseFactory(node.Operator, ConvertTo(left, opType), ConvertTo(right, opType));
                return Expression.Convert(combined, resultType);
            }

            if (NumericPromotion.IsIntegral(leftU) && NumericPromotion.IsIntegral(rightU) && !leftU.IsEnum && !rightU.IsEnum)
            {
                if (!NumericPromotion.TryPromote(leftU, rightU, out var promoted))
                    return FailBinary(node, left.Type, right.Type);
                var target = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                return BitwiseFactory(node.Operator, ConvertTo(left, target), ConvertTo(right, target));
            }

            return FailBinary(node, left.Type, right.Type);
        }

        private static Expression BitwiseFactory(ExprOperator op, Expression l, Expression r)
        {
            switch (op)
            {
                case ExprOperator.And: return Expression.And(l, r);
                case ExprOperator.ExclusiveOr: return Expression.ExclusiveOr(l, r);
                default: return Expression.Or(l, r);
            }
        }

        private Expression VisitLogical(BinaryNode node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);
            if (left == null || right == null)
                return null;

            if (left.Type != typeof(bool))
                return FailLogical(node, left.Type);
            if (right.Type != typeof(bool))
                return FailLogical(node, right.Type);

            return node.Operator == ExprOperator.AndAlso
                ? Expression.AndAlso(left, right)
                : Expression.OrElse(left, right);
        }

        private Expression FailLogical(BinaryNode node, Type type)
        {
            var op = node.Operator == ExprOperator.AndAlso ? "&&" : "||";
            return Fail(node.Position, HeddleDiagnosticIds.LogicalOperatorRequiresBool,
                $"Operator '{op}' requires bool operands, but the operand type is {FriendlyName(type)}.");
        }

        private Expression VisitCoalesce(BinaryNode node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);
            if (left == null || right == null)
                return null;

            var leftType = left.Type;
            var leftUnderlying = Nullable.GetUnderlyingType(leftType);
            if (leftType.IsValueType && leftUnderlying == null)
                return Fail(node.Position, HeddleDiagnosticIds.CoalesceLeftNotNullable,
                    $"Operator '??' requires a reference-type or Nullable<T> left operand, but the operand type is {FriendlyName(leftType)}.");

            if (IsNullLiteral(right))
                return Expression.Coalesce(left, Expression.Constant(null, leftType));

            if (leftUnderlying != null && NumericPromotion.IsNumeric(leftUnderlying))
            {
                var rightUnderlying = Nullable.GetUnderlyingType(right.Type) ?? right.Type;
                if (NumericPromotion.IsNumeric(rightUnderlying) && leftUnderlying != rightUnderlying)
                {
                    if (!NumericPromotion.TryPromote(leftUnderlying, rightUnderlying, out var promoted))
                        return Fail(node.Position, HeddleDiagnosticIds.TernaryArmsNoCommonType,
                            $"The conditional operator arms have no common type ({FriendlyName(leftType)} vs {FriendlyName(right.Type)}).");
                    var nullablePromoted = typeof(Nullable<>).MakeGenericType(promoted);
                    var l = ConvertTo(left, nullablePromoted);
                    var rightIsNullable = Nullable.GetUnderlyingType(right.Type) != null;
                    var r = ConvertTo(right, rightIsNullable ? nullablePromoted : promoted);
                    return Expression.Coalesce(l, r);
                }
            }

            try
            {
                return Expression.Coalesce(left, right);
            }
            catch (Exception)
            {
                return Fail(node.Position, HeddleDiagnosticIds.TernaryArmsNoCommonType,
                    $"The conditional operator arms have no common type ({FriendlyName(leftType)} vs {FriendlyName(right.Type)}).");
            }
        }

        #endregion

        #region Ternary

        private Expression VisitTernary(TernaryNode node)
        {
            var condition = Visit(node.Condition);
            var whenTrue = Visit(node.WhenTrue);
            var whenFalse = Visit(node.WhenFalse);
            if (condition == null || whenTrue == null || whenFalse == null)
                return null;

            if (condition.Type != typeof(bool))
                return Fail(node.Position, HeddleDiagnosticIds.TernaryConditionNotBool,
                    $"The conditional operator requires a bool condition, but the condition type is {FriendlyName(condition.Type)}.");

            if (!TryUnify(ref whenTrue, ref whenFalse))
                return Fail(node.Position, HeddleDiagnosticIds.TernaryArmsNoCommonType,
                    $"The conditional operator arms have no common type ({FriendlyName(node)} ).");

            return Expression.Condition(condition, whenTrue, whenFalse);
        }

        private bool TryUnify(ref Expression whenTrue, ref Expression whenFalse)
        {
            bool tNull = IsNullLiteral(whenTrue);
            bool fNull = IsNullLiteral(whenFalse);
            if (tNull && fNull)
                return false;
            if (tNull)
            {
                if (whenFalse.Type.IsValueType && Nullable.GetUnderlyingType(whenFalse.Type) == null)
                    return false;
                whenTrue = Expression.Constant(null, whenFalse.Type);
                return true;
            }

            if (fNull)
            {
                if (whenTrue.Type.IsValueType && Nullable.GetUnderlyingType(whenTrue.Type) == null)
                    return false;
                whenFalse = Expression.Constant(null, whenTrue.Type);
                return true;
            }

            var ta = whenTrue.Type;
            var tb = whenFalse.Type;
            if (ta == tb)
                return true;

            if (Nullable.GetUnderlyingType(tb) == ta)
            {
                whenTrue = Expression.Convert(whenTrue, tb);
                return true;
            }

            if (Nullable.GetUnderlyingType(ta) == tb)
            {
                whenFalse = Expression.Convert(whenFalse, ta);
                return true;
            }

            var au = Nullable.GetUnderlyingType(ta) ?? ta;
            var bu = Nullable.GetUnderlyingType(tb) ?? tb;
            if (NumericPromotion.IsNumeric(au) && NumericPromotion.IsNumeric(bu))
            {
                if (!NumericPromotion.TryPromote(au, bu, out var promoted))
                    return false;
                bool lifted = ta != au || tb != bu;
                var unified = lifted ? typeof(Nullable<>).MakeGenericType(promoted) : promoted;
                whenTrue = ConvertTo(whenTrue, unified);
                whenFalse = ConvertTo(whenFalse, unified);
                return true;
            }

            if (!ta.IsValueType && !tb.IsValueType)
            {
                if (ta.IsAssignableFrom(tb))
                {
                    whenFalse = Expression.Convert(whenFalse, ta);
                    return true;
                }

                if (tb.IsAssignableFrom(ta))
                {
                    whenTrue = Expression.Convert(whenTrue, tb);
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Helpers

        private static bool IsNullLiteral(Expression expression)
        {
            return expression is ConstantExpression constant && constant.Value == null;
        }

        /// <summary>
        /// Recognizes an <c>int</c> literal or a single sign-prefix over one (<c>-1</c>, <c>+2</c>) — the two
        /// argument shapes the HED4001 static step check evaluates. Any other shape (a member, <c>2 - 2</c>)
        /// returns false and defers to the render-time guard.
        /// </summary>
        private static bool TryGetLiteralIntStep(ExprNode node, out int value)
        {
            value = 0;
            if (node is LiteralNode literal && literal.Value is int direct)
            {
                value = direct;
                return true;
            }

            if (node is UnaryNode unary && unary.Operand is LiteralNode inner && inner.Value is int magnitude)
            {
                if (unary.Operator == ExprOperator.Negate)
                {
                    value = -magnitude;
                    return true;
                }

                if (unary.Operator == ExprOperator.UnaryPlus)
                {
                    value = magnitude;
                    return true;
                }
            }

            return false;
        }

        private static bool IsReferenceish(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private static Expression ConvertTo(Expression expression, Type target)
        {
            return expression.Type == target ? expression : Expression.Convert(expression, target);
        }

        private Expression FailBinary(BinaryNode node, Type leftType, Type rightType)
        {
            return Fail(node.Position, HeddleDiagnosticIds.BinaryOperatorNotDefined,
                $"Operator '{Symbol(node.Operator)}' is not defined for operand types {FriendlyName(leftType)} and {FriendlyName(rightType)}.");
        }

        private Expression Fail(BlockPosition position, string diagnosticId, string message)
        {
            _failed = true;
            _compileScope.CompileErrors.Add(message.ToError(position, diagnosticId));
            return null;
        }

        private static string Symbol(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Add: return "+";
                case ExprOperator.Subtract: return "-";
                case ExprOperator.Multiply: return "*";
                case ExprOperator.Divide: return "/";
                case ExprOperator.Modulo: return "%";
                case ExprOperator.LeftShift: return "<<";
                case ExprOperator.RightShift: return ">>";
                case ExprOperator.LessThan: return "<";
                case ExprOperator.LessThanOrEqual: return "<=";
                case ExprOperator.GreaterThan: return ">";
                case ExprOperator.GreaterThanOrEqual: return ">=";
                case ExprOperator.Equal: return "==";
                case ExprOperator.NotEqual: return "!=";
                case ExprOperator.And: return "&";
                case ExprOperator.ExclusiveOr: return "^";
                case ExprOperator.Or: return "|";
                default: return op.ToString();
            }
        }

        private static string FriendlyName(ExprNode _)
        {
            return "the two arms";
        }

        private static string FriendlyName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(char)) return "char";
            if (type == typeof(object)) return "object";
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return FriendlyName(underlying) + "?";
            return type.Name;
        }

        #endregion
    }
}
