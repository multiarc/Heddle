using Heddle.Strings.Core;

namespace Heddle.Language
{
    public enum HeddleTokenType
    {
        Id = 0,
        RootReference,
        MemberSelector,
        Out,
        SubStart,
        SubClose,
        CSharpToken,
        CSharpStart,
        DefStartName,
        DefEndName,
        DefType,
        Delim,
        DefStart,
        DefClose,
        Comment,
        OutParamStart,
        OutParamEnd,
        LineTerminate,
        DefOutputOnEnd,
        ParseError,
        // Native-expression kinds, appended at the end so the existing values stay stable.
        Operator,
        Literal,
        FunctionName
    }

    public class HeddleToken
    {
        public HeddleTokenType HeddleTokenType { get; set; }

        public BlockPosition Position { get; set; }
    }
}
