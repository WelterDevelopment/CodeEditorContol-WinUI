using CodeWriter_WinUI;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeWriter_WinUI_TestApp
{
    class ViewModel : Bindable
    {
        public string LastSavedText { get => Get("Hello\nWorld!"); set { Set(value); UnsavedChanges = value != Text; } }
        public string Text { get => Get("Hello\nWorld!"); set { Set(value); UnsavedChanges = value != LastSavedText; } }
        public int FontSize { get => Get(20); set => Set(value); }
        public int TabLength { get => Get(2); set => Set(value); }
        public bool ShowScrollbarMarkers { get => Get(true); set => Set(value); }
        public bool ShowLineMarkers { get => Get(true); set => Set(value); }
        public bool ShowLineNumbers { get => Get(true); set => Set(value); }
        public bool IsFoldingEnabled { get => Get(true); set => Set(value); }
        public bool ShowControlCharacters { get => Get(true); set => Set(value); }
        public bool UnsavedChanges { get => Get(false); set => Set(value); }

        public ElementTheme RequestedTheme { get => Get(ElementTheme.Default); set => Set(value); }
        public string Theme { get => Get("Default"); set { Set(value); RequestedTheme = (ElementTheme)Enum.Parse(typeof(ElementTheme), value); } }
        public string[] ThemeOptions => Enum.GetNames<ElementTheme>();

        public Language EditorLanguage { get => Get(LanguageList[1]); set { Set(value);  } }
        public string Language { get => Get("Lua"); set { Set(value); EditorLanguage = LanguageList.First(x=>x.Name == value); } }
        public string[] LanguageOptions => LanguageList.Select(x=>x.Name).ToArray();
        public List<Language> LanguageList = new() 
        {
            Languages.ConTeXt,
            new()
            {
                Name = "Lua",
                RegexTokens = new()
                {
                    { Token.Math, /*language=regex*/ @"\b(math)\.(pi|a?tan|atan2|tanh|a?cos|cosh|a?sin|sinh|max|pi|min|ceil|floor|(fr|le)?exp|pow|fmod|modf|random(seed)?|sqrt|log(10)?|deg|rad|abs)\b" },
                    { Token.Array, /*language=regex*/ @"\b((table)\.(insert|concat|sort|remove|maxn)|(string)\.(insert|sub|rep|reverse|format|len|find|byte|char|dump|lower|upper|g?match|g?sub|format|formatters))\b" },
                    { Token.Symbol, /*language=regex*/ @"[:=<>,.!?&%+\|\-*\/\^~;]" },
                    { Token.Bracket, /*language=regex*/ @"[\[\]\(\)\{\}]" },
                    { Token.Number, /*language=regex*/ @"0[xX][0-9a-fA-F]*|\d*\.\d+([eE][\-+]?\d+)?|\d+?" },
                    { Token.String, /*language=regex*/ "\\\".*?\\\"|'.*?'" },
                    { Token.Comment, /*language=regex*/ @"--.*" },
                },
                WordTokens = new()
                {
                    { Token.Keyword, new string[] { "local", "true", "false", "if", "elseif", "in" , "else", "not", "or", "and", "then", "nil", "end", "function", "for", "do", "while", "repeat", "goto", "until", "return", "break" } },
                    { Token.Environment, new string[] { "function", "end", "if", "elseif", "else", "while", "for",  } },
                    { Token.Function, new string[] { "#", "assert", "collectgarbage", "dofile", "_G", "getfenv", "ipairs", "load", "loadstring", "pairs", "pcall", "print", "rawequal", "rawget", "rawset", "select", "setfenv", "_VERSION", "xpcall", "module", "require", "tostring", "tonumber", "type", "rawset", "setmetatable", "getmetatable", "error", "unpack", "next",  } }

                },      
            }
        };

        public string Log { get => Get(""); set => Set(value); }
    }
}
