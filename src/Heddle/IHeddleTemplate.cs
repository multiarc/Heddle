using System;
using System.IO;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle
{
    public interface IHeddleTemplate : IDisposable
    {
        HeddleCompileResult CompileResult { get; }
        bool Empty { get; }
        bool Compiled { get; }

        CompileContext Context { get; }

        /// <summary>
        /// Generates result string (invoke template helpers and render).
        /// </summary>
        /// <param name="data">Input object</param>
        /// <param name="callerData"></param>
        /// <param name="chained"></param>
        /// <returns>Generated string</returns>
        string Generate(object data, object chained = null, object callerData = null);

        HeddleCompileResult Recompile(ExType newModelType);

        HeddleCompileResult Recompile(string newDocument, CompileContext context = null);

        HeddleCompileResult Compile(string document, ExType modelType = null);

        HeddleCompileResult Compile(CompileContext context);

        HeddleCompileResult TryCompilation(string document, TemplateOptions options = null, ExType modelType = null);

        HeddleCompileResult TryCompilation(CompileContext context);

        event FileSystemEventHandler OnFileDeleted;
        event RenamedEventHandler OnFileRenamed;
        event FileSystemEventHandler OnFileChanged;
    }
}