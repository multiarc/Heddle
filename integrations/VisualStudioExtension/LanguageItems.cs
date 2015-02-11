using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Templater.VisualStudioExtension {
   abstract class LanguageKeywords {
      private readonly Dictionary<string, string[]> _keywords =
         new Dictionary<string, string[]>();

      public string[] ControlFlow {
         get { return Get("ControlFlow", ControlFlowDefaults); }
      }
      public string[] Linq {
         get { return Get("Linq", LinqDefaults); }
      }
      public string[] Visibility {
         get { return Get("Visibility", VisibilityDefaults); }
      }

      protected abstract string[] ControlFlowDefaults { get; }
      protected abstract string[] LinqDefaults { get; }
      protected abstract string[] VisibilityDefaults { get; }
      protected abstract string KeyName { get; }

      protected string[] Get(string name, string[] defaults) {
         if ( !_keywords.ContainsKey(name) ) {
            string[] values = 
               ConfigHelp.GetValue(KeyName + "_" + name, "").AsList();
            if ( values == null || values.Length == 0 )
               values = defaults;
            _keywords[name] = values;
         }
         return _keywords[name];
      }
   }

   static class ConfigHelp {
       private const string RegKey = "Software\\Winterdom\\VS Extensions\\KeywordClassifier";

       public static string GetValue(string name, string defValue)
       {
           using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKey))
           {
               if (key != null)
               {
                   string value = key.GetValue(name, defValue) as string;
                   if (string.IsNullOrEmpty(defValue)) value = defValue;
                   return value;
               }
           }
           return string.Empty;
       }
   }

   static class StringExtensions {
      public static string[] AsList(this string str) {
         return str.Split(new Char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
      }
   }

   class CSharp : LanguageKeywords {
      public const string ContentType = "CSharp";
      static readonly string[] CsKeywords = {
         "if", "else", "while", "do", "for", "foreach", 
         "switch", "break", "continue", "return", "goto", "throw" 
      };
      static readonly string[] CsLinqKeywords = {
         "select", "let", "where", "join", "orderby", "group",
         "by", "on", "equals", "into", "from", "descending",
         "ascending"
      };
      static readonly string[] CsVisKeywords = {
         "public", "private", "protected", "internal"
      };
      protected override string[] ControlFlowDefaults {
         get { return CsKeywords; }
      }
      protected override string[] LinqDefaults {
         get { return CsLinqKeywords; }
      }
      protected override string[] VisibilityDefaults {
         get { return CsVisKeywords; }
      }
      protected override string KeyName {
         get { return "CSharp"; }
      }
   }
   class JScript : LanguageKeywords {
      public const string ContentType = "JScript";
      public const string ContentTypeVs2012 = "JavaScript";

      static readonly string[] JsKeywords = {
         "if", "else", "while", "do", "for", "switch",
         "break", "continue", "return", "throw"
      };
      static readonly string[] JsLinqKeywords = {
         "in", "with"
      };
      protected override string[] ControlFlowDefaults
      {
         get { return JsKeywords; }
      }
      protected override string[] LinqDefaults {
         get { return JsLinqKeywords; }
      }
      protected override string[] VisibilityDefaults {
         get { return new string[0]; }
      }
      protected override string KeyName {
         get { return "JScript"; }
      }
   }
}
