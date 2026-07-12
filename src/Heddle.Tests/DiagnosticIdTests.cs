using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Pins the phase 1 diagnostic-ID plumbing (cross-cutting D1): the null-preserving ToString formats and
    /// the one-to-one match between HeddleDiagnosticIds constants and the Diagnostics table.
    /// </summary>
    public class DiagnosticIdTests
    {
        [Fact]
        public void ToStringOmitsIdWhenNull()
        {
            var error = new HeddleCompileError { Error = "boom", Position = new BlockPosition(0, 0) };
            Assert.Equal("[0:0]boom\r\n", error.ToString());
        }

        [Fact]
        public void ToStringIncludesIdWhenSet()
        {
            var error = new HeddleCompileError
            {
                Error = "boom",
                Position = new BlockPosition(3, 2),
                DiagnosticId = HeddleDiagnosticIds.MethodCallNotAvailable
            };
            Assert.Equal("[3:2]HED1003: boom\r\n", error.ToString());
        }

        [Fact]
        public void ToStringExplicitOverloadIncludesId()
        {
            var error = new HeddleCompileError
            {
                Error = "boom",
                DiagnosticId = HeddleDiagnosticIds.SyntaxError
            };
            Assert.Equal("[1,2:3]HED0003: boom\r\n", error.ToString(1, 2, 3));
        }

        [Fact]
        public void ConstantsMatchTheDiagnosticsTableOneToOne()
        {
            var expected = new HashSet<string>
            {
                "HED0001", "HED0002", "HED0003", "HED0004",
                "HED1001", "HED1002", "HED1003", "HED1004", "HED1005", "HED1006", "HED1007",
                "HED1008", "HED1009", "HED1010", "HED1011", "HED1012", "HED1013", "HED1014",
                "HED1015", "HED1016", "HED1017",
                "HED2001", "HED2002", "HED2003",
                "HED3001", "HED3002", "HED3003", "HED3004", "HED3005",
                "HED4001", "HED4002", "HED4003", "HED4004",
                "HED5001", "HED5002", "HED5003", "HED5004", "HED5005", "HED5006",
                "HED5007", "HED5008", "HED5009", "HED5010", "HED5011", "HED5012",
                "HED5013", "HED5014", "HED5015", "HED5016", "HED5017", "HED5018"
            };

            var constants = typeof(HeddleDiagnosticIds)
                .GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();

            Assert.Equal(expected.Count, constants.Count);
            Assert.Equal(expected, new HashSet<string>(constants));
            Assert.Equal(constants.Count, constants.Distinct().Count());
        }
    }
}
