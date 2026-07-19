using System.Collections.Generic;
using Fluid.Benchmarks;

namespace Heddle.Performance.ThirdParty
{
    /// <summary>
    /// Typed root model for the Heddle entry. It carries the <em>exact same</em>
    /// <see cref="Product"/> list the upstream harness builds in <see cref="BaseBenchmarks"/>,
    /// exposed under the name <c>Products</c> — the direct analogue of the <c>products</c>
    /// variable Fluid/Scriban/DotLiquid/Handlebars each bind before rendering. Same data, same
    /// count, no methodology change; only the (idiomatic, statically typed) shape differs.
    /// </summary>
    public sealed class HeddleProductModel
    {
        public HeddleProductModel(List<Product> products)
        {
            Products = products;
        }

        public List<Product> Products { get; }
    }
}
