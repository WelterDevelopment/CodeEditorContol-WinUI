using CodeEditorControl_WinUI;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeEditor_WinUI_TestApp
{
  internal class ViewModel : Bindable
  {
    public List<Language> LanguageList = new()
    {
      new("Lua")
      {
        FoldingPairs = new()
        {
          new() { RegexStart = /*language=regex*/ @"\bfunction\b", RegexEnd = /*language=regex*/ @"\bend\b" },
          new() { RegexStart = /*language=regex*/ @"\bfor\b", RegexEnd = /*language=regex*/ @"\bend\b" },
          new() { RegexStart = /*language=regex*/ @"\bwhile\b", RegexEnd = /*language=regex*/ @"\bend\b" },
          new() { RegexStart = /*language=regex*/ @"\bif\b", RegexEnd = /*language=regex*/ @"\bend\b" },
        },
        RegexTokens = new()
        {
          { Token.Math, /*language=regex*/ @"\b(math)\.(pi|a?tan|atan2|tanh|a?cos|cosh|a?sin|sinh|max|pi|min|ceil|floor|(fr|le)?exp|pow|fmod|modf|random(seed)?|sqrt|log(10)?|deg|rad|abs)\b" },
          { Token.Array, /*language=regex*/ @"\b((table)\.(insert|concat|sort|remove|maxn)|(string)\.(insert|sub|rep|reverse|format|len|find|byte|char|dump|lower|upper|g?match|g?sub|format|formatters))\b" },
          { Token.Symbol, /*language=regex*/ @"[:=<>,.!?&%+\|\-*\/\^~;]" },
          { Token.Bracket, /*language=regex*/ @"[\[\]\(\)\{\}]" },
          { Token.Number, /*language=regex*/ @"0[xX][0-9a-fA-F]*|-?\d*\.\d+([eE][\-+]?\d+)?|-?\d+?" },
          { Token.String, /*language=regex*/ "\\\".*?\\\"|'.*?'" },
          { Token.Comment, /*language=regex*/ @"--.*" },
        },
        WordTokens = new()
        {
          { Token.Keyword, new string[] { "local", "true", "false", "in", "else", "not", "or", "and", "then", "nil", "end", "do", "repeat", "goto", "until", "return", "break" } },
          { Token.Environment, new string[] { "function", "end", "if", "elseif", "else", "while", "for", } },
          { Token.Function, new string[] { "#", "assert", "collectgarbage", "dofile", "_G", "getfenv", "ipairs", "load", "loadstring", "pairs", "pcall", "print", "rawequal", "rawget", "rawset", "select", "setfenv", "_VERSION", "xpcall", "module", "require", "tostring", "tonumber", "type", "rawset", "setmetatable", "getmetatable", "error", "unpack", "next", } }
        },
      }
    };

    public Language EditorLanguage { get => Get(LanguageList[0]); set { Set(value); } }
    public int FontSize { get => Get(20); set => Set(value); }
    public bool IsFoldingEnabled { get => Get(true); set => Set(value); }
    public bool IsWrappingEnabled { get => Get(true); set => Set(value); }
    public string Language { get => Get("Lua"); set { Set(value); EditorLanguage = LanguageList.First(x => x.Name == value); } }
    public string[] LanguageOptions => LanguageList.Select(x => x.Name).ToArray();
    public string LastSavedText { get => Get(""); set { Set(value); UnsavedChanges = value != Text; } }
    public string Log { get => Get(""); set => Set(value); }
    public ElementTheme RequestedTheme { get => Get(ElementTheme.Default); set => Set(value); }
    public bool ShowControlCharacters { get => Get(true); set => Set(value); }
    public bool ShowHorizontalTicks { get => Get(true); set => Set(value); }
    public bool ShowLineMarkers { get => Get(true); set => Set(value); }
    public bool ShowLineNumbers { get => Get(true); set => Set(value); }
    public bool ShowScrollbarMarkers { get => Get(true); set => Set(value); }
    public int TabLength { get => Get(2); set => Set(value); }
    public string Text { get => Get(""); set { Set(value); UnsavedChanges = value != LastSavedText; } }
    public string Theme { get => Get("Default"); set { Set(value); RequestedTheme = (ElementTheme)Enum.Parse(typeof(ElementTheme), value); } }
    public string[] ThemeOptions => Enum.GetNames<ElementTheme>();
    public bool UnsavedChanges { get => Get(false); set => Set(value); }
  }
}