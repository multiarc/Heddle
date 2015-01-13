using System;
using IDE.Helpers;

namespace IDE.UI {
    internal class TextBoxContext {
        public TextBoxContext (string fileName)
        {
            FileName = fileName;
        }

        public bool IsChanged
        {
            get;
            private set;
        }

        public string FileName
        {
            get;
            private set;
        }

        public void Changed (object sender, EventArgs e)
        {
            IsChanged = true;
        }

        public void Save (string document)
        {
            FileHelper.WriteTextFile(FileName, document);
            IsChanged = false;
        }

        public void Save (string document, string fileName)
        {
            FileName = fileName;
            Save(document);
        }
    }
}