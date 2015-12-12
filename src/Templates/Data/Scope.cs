using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Templates.Data
{
    public class Scope
    {
        public readonly object ModelData;
        public readonly object ChainedData;
        public readonly object ParentModelData;
        public readonly dynamic CallerData;

        internal readonly object RootData;

        internal Scope(object root, dynamic data, object model, object chained, object parent = null)
        {
            RootData = root;
            ModelData = model;
            ChainedData = chained;
            ParentModelData = parent;
            CallerData = data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Parent()
        {
            return new Scope(RootData, CallerData, ParentModelData, ChainedData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Parent(object chained)
        {
            return new Scope(RootData, CallerData, ParentModelData, chained);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Chain(object chained)
        {
            return new Scope(RootData, CallerData, ModelData, chained, ModelData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Model(object model)
        {
            return new Scope(RootData, CallerData, model, ChainedData, ModelData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Model(object model, object chained)
        {
            return new Scope(RootData, CallerData, model, chained, ModelData);
        }

        public static readonly Scope Null = new Scope(null, null, null, null);
    }
}
