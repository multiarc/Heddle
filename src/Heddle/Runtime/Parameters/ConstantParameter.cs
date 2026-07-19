using System.Runtime.CompilerServices;
using Heddle.Data;

namespace Heddle.Runtime.Parameters
{
    internal class ConstantParameter : IRuntimeParameter
    {
        private readonly object _constantResult;

        public ConstantParameter(object constantResult)
        {
            _constantResult = constantResult;
        }

        /// <summary>The folded constant value (phase 5 reads this to bake constant prop arguments into the
        /// frozen prototype at compile time).</summary>
        internal object Value => _constantResult;

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            return _constantResult;
        }
    }
}