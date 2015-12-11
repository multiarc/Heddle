using System;
using System.IO;
using Templates.Data;
using Templates.Runtime;

namespace Templates
{
    public interface ITtlTemplate : IDisposable
    {
        TtlCompileResult CompileResult { get; }
        bool Empty { get; }
        bool Compiled { get; }

        DateTime DateCreated { get; set; }

        DateTime MasterDateCreated { get; set; }

        CompileContext Context { get; }
        /// <summary>
        /// Generates result string (invoke template helpers and render).
        /// </summary>
        /// <param name="data">Input object</param>
        /// <returns>Generated string</returns>
        string Generate(object data, object chained = null);

        TtlCompileResult Recompile(ExType newModelType);

        TtlCompileResult Recompile(string newDocument, CompileContext context = null);

        TtlCompileResult Compile(string document, ExType modelType = null);

        TtlCompileResult Compile(CompileContext context);    

        TtlCompileResult TryCompilation(string document, ExType modelType = null);

        TtlCompileResult TryCompilation(CompileContext context);

        event FileSystemEventHandler OnFileDeleted;
        event RenamedEventHandler OnFileRenamed;
        event FileSystemEventHandler OnFileChanged;
    }
}