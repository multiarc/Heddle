using Templates.Strings.Core;

namespace Templates.Language
{
    public enum TtlTokenType
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
        ParseError
    }

    public class TtlToken
    {
        public TtlTokenType TtlTokenType { get; set; }

        public BlockPosition Position { get; set; }
    }
}
