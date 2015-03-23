using System;
using System.Globalization;
using System.IO;
using Templates.Data;
using Templates.Exceptions;

namespace Templates {
    public class FileReader {
        private readonly TemplateOptions _options;
        private readonly string _templateName;

        public FileReader (TemplateOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FileNamePostfix))
                throw new ArgumentException("File Name postfix (extension) should not be empty");
            if (string.IsNullOrWhiteSpace(options.RootPath))
                throw new ArgumentException("Root Path (directory) should not be empty");
            if (string.IsNullOrWhiteSpace(options.TemplateName))
                throw new ArgumentException("Template Name should not be empty");

            _templateName = options.TemplateName;
            _options = options;
        }

        public string ReadEntireFile ()
        {
            string fileName = GetFileName();
            try {
                using (StreamReader reader = File.OpenText(fileName)) {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception e) {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "File not found [{0}].", fileName), e);
            }
        }

        public string GetFileName ()
        {
            return Path.Combine(_options.RootPath, _templateName + _options.FileNamePostfix);
        }
    }
}