using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Strings;

namespace Heddle.Extensions
{
    /// <summary>
    /// Renders a sub-template once per element of an <see cref="IEnumerable{T}"/>, concatenating results.
    /// </summary>
    [ExtensionName("list")]
    [DataType(typeof(IEnumerable))]
    public class ListExtension : AbstractExtension
    {
        private ICountReader _collectionCountReader;

        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "CountReader<T> is instantiated only over reference-type elements, which share one canonical instantiation in an AOT publish; value-type elements take the non-generic count path.")]
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));
            string ambiguity;
            if (HasAmbiguousElementType(dataType.Type, out ambiguity))
                NoteOrderRefusal(initContext, ambiguity);
            if (dataType.IsDynamic)
            {
                return base.InitStart(initContext, dataType, new ExType(typeof(int)), null);
            }

            var elementType = dataType.Type.TryGetElementType(typeof(ICollection<>));
            if (elementType != null)
            {
                // CountReader<T> shares one instantiation for reference-type elements, but a
                // value-type element needs fresh codegen per T, which NativeAOT cannot make at load.
                // Value-type elements take the non-generic ICollection.Count path instead (else no
                // count, which only loses the result-array pre-size, never a byte).
                if (elementType.IsValueType)
                {
                    _collectionCountReader = NonGenericCountReader.Instance;
                }
                else
                {
                    // Reference-type instantiations share codegen, so this MakeGenericType is
                    // AOT-safe; value-type elements never reach it.
                    _collectionCountReader = (ICountReader) Activator.CreateInstance(typeof(CountReader<>).MakeGenericType(elementType));
                }
            }

            ExType underlyingType = dataType.Type.TryGetElementType(typeof(IEnumerable<>)) ?? ExType.Dynamic;
            return base.InitStart(initContext, underlyingType, new ExType(typeof(int)), parent);
        }

        public override object ProcessData(in Scope scope)
        {
            if (!(scope.ModelData is IEnumerable))
                return string.Empty;
            // Type-test the probe once; large loops accumulate before reaching the sink, so enforce the deadline here.
            var probe = scope.Renderer as IBudgetProbe;
            var enumerable = (IEnumerable) scope.ModelData;
            var count = _collectionCountReader?.GetCount(scope.ModelData);
            if (count.HasValue)
            {
                var totalLength = 0;
                var itemResults = new string[count.Value];
                var index = 0;

                foreach (var item in enumerable)
                {
                    probe?.TickDeadline();
                    var itemScope = scope.Model(item, index);
                    // A bodiless @list has no inner result at all; the slot still has to hold a string,
                    // because the concatenation below copies every slot.
                    var result = GetInnerResult(itemScope) ?? string.Empty;
                    itemResults[index] = result;
                    totalLength += result.Length;
                    index++;
                }

                return ExStringBuilder.Concat(itemResults, count.Value, totalLength);
            }
            else
            {
                var itemResults = new LinearList<string>();
                var totalLength = 0;
                var index = 0;

                foreach (var item in enumerable)
                {
                    probe?.TickDeadline();
                    var itemScope = scope.Model(item, index);
                    var result = GetInnerResult(itemScope);
                    if (!string.IsNullOrEmpty(result))
                    {
                        itemResults.Add(result);
                        totalLength += result.Length;
                    }

                    index++;
                }
                return ExStringBuilder.Concat(itemResults.Array, itemResults.Count, totalLength);
            }
        }

        public override void RenderData(in Scope scope)
        {
            if (!(scope.ModelData is IEnumerable))
                return;

            // Type-test the probe once; enforce the deadline per iteration even if the loop produces no output.
            var probe = scope.Renderer as IBudgetProbe;
            var enumerable = (IEnumerable) scope.ModelData;
            var index = 0;
            foreach (var item in enumerable)
            {
                probe?.TickDeadline();
                var itemScope = scope.Model(item, index);
                RenderInnerResult(itemScope);
                index++;
            }
        }

        private interface ICountReader
        {
            int? GetCount(object value);
        }

        private struct CountReader<T> : ICountReader
        {
            public int? GetCount(object value)
            {
                return (value as ICollection<T>)?.Count;
            }
        }

        /// <summary>Reference-free count for value-type element types: the non-generic
        /// <see cref="System.Collections.ICollection.Count"/>, else no count (the loop then grows a
        /// <c>LinearList</c> instead of a pre-sized array; the bytes never change).</summary>
        private sealed class NonGenericCountReader : ICountReader
        {
            public static readonly NonGenericCountReader Instance = new NonGenericCountReader();

            public int? GetCount(object value)
            {
                return (value as System.Collections.ICollection)?.Count;
            }
        }

        /// <summary>Detects the reflection-order hazard: the element type the engine would pick is the
        /// first of several <c>IEnumerable&lt;T&gt;</c> implementations in reflection enumeration order,
        /// so no static answer exists. The build records a class-(b) refusal instead of blessing one.</summary>
        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection over a model type; model types reach the engine through [HeddleModelAssembly]/typeof parameters annotated DynamicallyAccessedMemberTypes.All, which keeps their members through a trimmed publish.")]
        private static bool HasAmbiguousElementType(Type type, out string detail)
        {
            detail = null;
            if (type == null)
                return false;
            var candidates = type.GetTypeInfo().ImplementedInterfaces
                .Where(i => i.GetTypeInfo().IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(i => i.GenericTypeArguments[0])
                .Distinct()
                .ToList();
            if (candidates.Count < 2)
                return false;
            detail = "element type chosen among " +
                string.Join(", ", candidates.Select(t => t.FullName));
            return true;
        }

        private static void NoteOrderRefusal(InitContext initContext, string detail)
        {
            var scope = initContext.CompileScope;
            var record = scope?.CompileContext.FormRecord;
            if (record == null || initContext.SourceItem == null)
                return;
            record.NoteRefusal(initContext.SourceItem, PrecompiledRefusalClass.ReflectionOrderValue,
                detail, scope.ScopeType, scope.ScopeType, scope.RootScopeType,
                new List<string>(scope.Namespaces));
        }
    }
}