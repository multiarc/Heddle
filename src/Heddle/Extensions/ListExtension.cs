using System;
using System.Collections;
using System.Collections.Generic;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Helpers;
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

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));
            if (dataType.IsDynamic)
            {
                return base.InitStart(initContext, dataType, new ExType(typeof(int)), null);
            }

            var elementType = dataType.Type.TryGetElementType(typeof(ICollection<>));
            if (elementType != null)
            {
                _collectionCountReader = (ICountReader) Activator.CreateInstance(typeof(CountReader<>).MakeGenericType(elementType));
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
                    var result = GetInnerResult(itemScope);
                    itemResults[index] = result;
                    totalLength += result?.Length ?? 0;
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
    }
}