using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace Templates.Language.ParserExtensions
{
    public static class ParserGetTextExtension
    {
        public static string GetInnerText(this ParserRuleContext context)
        {
            return context.Start?.InputStream == null || context.Start.StopIndex + 1 == context.Stop.StartIndex - 1
                ? string.Empty
                : context.Start.InputStream.GetText(new Interval(context.Start.StopIndex + 1, context.Stop.StartIndex - 1));
        }
    }
}