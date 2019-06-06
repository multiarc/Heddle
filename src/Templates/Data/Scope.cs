using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Templates.Data
{
    public class ScopeRenderer
    {
        private readonly List<string> _items;

        public ScopeRenderer(int elementCount = 0)
        {
            _items = new List<string>(0);
        }

        public int TotalCount => _items.Count;

        public void RenderNext(string data)
        {
            _items.Add(data ?? string.Empty);
        }
    }

    public struct Scope
    {
        public readonly object ModelData;
        public readonly object ChainedData;
        public readonly object ParentModelData;
        public readonly object CallerData;
        public readonly ScopeRenderer Renderer;
        internal readonly object RootData;

        internal Scope(object root, object data, object model, object chained, ScopeRenderer renderer,
            object parent = null)
        {
            RootData = root;
            ModelData = model;
            ChainedData = chained;
            ParentModelData = parent;
            CallerData = data;
            Renderer = renderer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Parent()
        {
            return new Scope(RootData, CallerData, ParentModelData, ChainedData, Renderer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Parent(object chained)
        {
            return new Scope(RootData, CallerData, ParentModelData, chained, Renderer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Chain(object chained)
        {
            return new Scope(RootData, CallerData, ModelData, chained, Renderer, ModelData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Model(object model)
        {
            return new Scope(RootData, CallerData, model, ChainedData, Renderer, ModelData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Model(object model, object chained)
        {
            return new Scope(RootData, CallerData, model, chained, Renderer, ModelData);
        }

        public static readonly Scope Null = new Scope(null, null, null, null, null);
    }
}
