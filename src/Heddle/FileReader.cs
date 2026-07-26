using System;
using System.Globalization;
using System.IO;
using Heddle.Data;

namespace Heddle {
    public class FileReader {
        private readonly TemplateOptions _options;

        public FileReader (TemplateOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FileNamePostfix))
                throw new ArgumentException("File Name postfix (extension) should not be empty");
            if (string.IsNullOrWhiteSpace(options.RootPath))
                throw new ArgumentException("Root Path (directory) should not be empty");
            if (string.IsNullOrWhiteSpace(options.TemplateName))
                throw new ArgumentException("Template Name should not be empty");

            _options = options;
        }

        public string ReadEntireFile ()
        {
            string fileName = GetFileName();
            try
            {
                using StreamReader reader = File.OpenText(fileName);
                return reader.ReadToEnd();
            }
            catch (Exception e) {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "File not found [{0}].", fileName), e);
            }
        }

        /// <summary>The composed file path from <see cref="TemplateOptions.FullPath"/>. The constructor guarantees non-blank inputs.</summary>
        public string GetFileName ()
        {
            return _options.FullPath;
        }
    }
}