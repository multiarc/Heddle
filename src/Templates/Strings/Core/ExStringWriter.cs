using System.IO;
using System.Text;

namespace Templates.Strings.Core {
    public class ExStringWriter : TextWriter
    {
        private readonly ExStringBuilder _builder = new ExStringBuilder();

        public override Encoding Encoding { get; } = new UnicodeEncoding(false, false);

        public override void Write(string value)
        {
            _builder.Append(value);
        }

        public override void Write(char value)
        {
            _builder.Append(new ExString(new[] {value}));
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}