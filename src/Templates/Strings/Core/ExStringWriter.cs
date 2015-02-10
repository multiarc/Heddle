using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Templates.Strings.Core {
    public class ExStringWriter : TextWriter
    {
        private readonly ExStringBuilder _builder = new ExStringBuilder();
        private readonly Encoding _encoding = new UnicodeEncoding(false, false);

        public override Encoding Encoding
        {
            get { return _encoding; }
        }

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
