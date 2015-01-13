using System.IO;
using System.Linq;
using System.Text;

namespace IDE.Helpers {
    internal static class FileHelper {
        public static string ReadTextFile (string fileName)
        {
            StreamReader reader = File.OpenText(fileName);
            string text = reader.ReadToEnd();
            if (text.Contains('�')) {
                reader.Close();
                reader = new StreamReader(fileName, Encoding.Unicode);
                text = reader.ReadToEnd();
                if (text.Contains('')) {
                    reader.Close();
                    reader = new StreamReader(fileName, Encoding.Default);
                    text = reader.ReadToEnd();
                }
            }
            reader.Close();
            return text;
        }

        public static string ReadTextFile (string fileName, Encoding encoding)
        {
            var reader = new StreamReader(fileName, encoding);
            string text = reader.ReadToEnd();
            reader.Close();
            return text;
        }

        public static void WriteTextFile (string fileName, string text)
        {
            StreamWriter writer = File.CreateText(fileName);
            writer.Write(text);
            writer.Close();
        }
    }
}