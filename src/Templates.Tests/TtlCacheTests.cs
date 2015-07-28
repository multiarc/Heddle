using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Templates.Tests
{
    public class TtlCacheTests
    {
        [Fact]
        public void TestCacheConstruction()
        {
            TtlGlobalCache cache = new TtlGlobalCache();
        }
    }
}
