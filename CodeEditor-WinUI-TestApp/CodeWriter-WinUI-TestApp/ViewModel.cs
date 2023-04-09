using CodeEditorControl_WinUI;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;

namespace CodeEditor_WinUI_TestApp
{
    internal class ViewModel : Bindable
	{
		public static List<Language> LanguageList = new()
		{
			new("ConTeXt")
			{
				EnableIntelliSense = true,
				LineComment = "%",
				EscapeSymbols = new [] { '\\' },
				AutoClosingPairs = new() { { '[', ']' }, { '{', '}' }, },
				WordSelectionDefinitions = new() { /*language=regex*/ @"\b\w+?\b", /*language=regex*/ @"\\.+?\b" },
				RegexTokens = new()
				{
					{ Token.Key, /*language=regex*/ @"(\w+?\s*?)(=)" },
						{ Token.Math, /*language=regex*/ @"\${1,2}.*?\${1,2}" },
					{ Token.Symbol, /*language=regex*/ @"[:=,.!?&+\-*\/\^~#;<>]" },
					{ Token.Command, /*language=regex*/ @"\\.+?\b" },
					{ Token.Function, /*language=regex*/ @"\\(define|place|enable|setup).+?\b" },
					{ Token.Style, /*language=regex*/ @"\\(tf|bf|it|sl|bi|bs|sc)(x|xx|[a-e])?\b|(\\tt|\\ss|\\rm)\b" },
					{ Token.Array, /*language=regex*/ @"\\\\|\\(b|e)(T)(C|Ds?|H|N|Rs?|X|Y)\b|(\\AR|\\DR|\\DC|\\DL|\\NI|\\NR|\\NC|\\HL|\\VL|\\FR|\\MR|\\LR|\\SR|\\TB|\\NB|\\NN|\\FL|\\ML|\\LL|\\TL|\\BL)\b" },
					{ Token.Environment, /*language=regex*/ @"\\(start|stop).+?\b" },
					{ Token.Reference, /*language=regex*/ @"(\#+?\d+|\w+?)(:(\#+?\d+|\w+?)\b)+|\\ref|\#+?\d+?\b" },
					{ Token.Bracket, /*language=regex*/ @"(?<!\\)(\[|\]|\(|\)|\{|\})" },
					//{ Token.Comment, /*language=regex*/ @"\%.*" },
				},
				WordTokens = new()
				{
					{ Token.Special, new string[] { @"\environment", @"\product", @"\component", @"\project", @"\input", @"\usemodule" } },
					{ Token.Primitive, new string[] { @"\vfill", @"\vfil", @"\hfill", @"\hfil", } },
				},
				FoldingPairs = new()
				{
					new SyntaxFolding() { RegexStart = /*language=regex*/ @"(\\start)(.+?)(\b)", RegexEnd = /*language=regex*/ @"(\\stop)(.+?)(\b)", FoldingIgnoreWords = new(){ "product", "environment", "component", "project", "text" }, MatchingGroup = 2 },
				},
				CommandTriggerCharacters = new[] { '\\' },
				OptionsTriggerCharacters = new[] { '[' },
				NestedLanguages = new()
				{
					new() { InnerLanguage = "Lua", RegexStart = /*language=regex*/ @"\\startlua(code)?\b", RegexEnd = /*language=regex*/ @"\\stoplua(code)?\b" }
				},
			},

			new("Lua")
			{
				FoldingPairs = new()
				{
					new() { RegexStart = /*language=regex*/ @"\b(function|for|while|if)\b", RegexEnd = /*language=regex*/ @"\bend\b" },
				},
				RegexTokens = new()
				{
					{ Token.Math, /*language=regex*/ @"\b(math)\.(pi|a?tan|atan2|tanh|a?cos|cosh|a?sin|sinh|max|pi|min|ceil|floor|(fr|le)?exp|pow|fmod|modf|random(seed)?|sqrt|log(10)?|deg|rad|abs)\b" },
					{ Token.Array, /*language=regex*/ @"\b((table)\.(insert|concat|sort|remove|maxn)|(string)\.(insert|sub|rep|reverse|format|len|find|byte|char|dump|lower|upper|g?match|g?sub|format|formatters))\b" },
					{ Token.Symbol, /*language=regex*/ @"[:=<>,.!?&%+\|\-*\/\^~;]" },
					{ Token.Bracket, /*language=regex*/ @"[\[\]\(\)\{\}]" },
					{ Token.Number, /*language=regex*/ @"0[xX][0-9a-fA-F]*|-?\d*\.\d+([eE][\-+]?\d+)?|-?\d+?" },
					{ Token.String, /*language=regex*/"\\\".*?\\\"|'.*?'" },
					{ Token.Comment, /*language=regex*/"\\\"[^\\\"]*\\\" | --.*?\\\n" },
				},
				WordTokens = new()
				{
					{ Token.Keyword, new string[] { "local", "true", "false", "in", "else", "not", "or", "and", "then", "nil", "end", "do", "repeat", "goto", "until", "return", "break" } },
					{ Token.Environment, new string[] { "function", "end", "if", "elseif", "else", "while", "for", } },
					{ Token.Function, new string[] { "#", "assert", "collectgarbage", "dofile", "_G", "getfenv", "ipairs", "load", "loadstring", "pairs", "pcall", "print", "rawequal", "rawget", "rawset", "select", "setfenv", "_VERSION", "xpcall", "module", "require", "tostring", "tonumber", "type", "rawset", "setmetatable", "getmetatable", "error", "unpack", "next", } }
				},
			},
			new("Log")
			{
				FoldingPairs = new()
				{

				},
				RegexTokens = new()
				{
					{ Token.Keyword, /*language=regex*/ @"^[\w ]*?(?=>)" },
					{ Token.Command, /*language=regex*/ @"\\.+?\b" },
					{ Token.Symbol, /*language=regex*/ @"[:=<>,.!?&%+\|\-*\/\^~;]" },
					{ Token.Bracket, /*language=regex*/ @"[\[\]\(\)\{\}]" },
					{ Token.Number, /*language=regex*/ @"0[xX][0-9a-fA-F]*|-?\d*\.\d+([eE][\-+]?\d+)?|-?\d+?" },
				},
				WordTokens = new()
				{

				},
			},
			new("Markdown")
			{
				FoldingPairs = new()
				{

				},
				RegexTokens = new()
				{
					{ Token.Environment, /*language=regex*/ @"^\s*?#+? .*" },
					{ Token.Keyword, /*language=regex*/ @"^[\w ]*?(?=>)" },
					{ Token.Command, /*language=regex*/ @"(?<=<\/|<)\w+?\b(?=.*?\/?>)" },
					{ Token.Function, /*language=regex*/ @"\[.*?\]" },
					{ Token.Key, /*language=regex*/ @"(?<=\s)\w+?\s*?(?==)" },
					{ Token.Comment, /*language=regex*/ @"^\s*?> .*" },
					{ Token.String, /*language=regex*/ @"'.*?'" },
					{ Token.Symbol, /*language=regex*/ @"[:=<>,.!?&%+\|\-*\/\^~;´`]" },
					{ Token.Bracket, /*language=regex*/ @"[\[\]\(\)\{\}]" },
					{ Token.Number, /*language=regex*/ @"0[xX][0-9a-fA-F]*\b|-?\d*\.\d+([eE][\-+]?\d+)?\b|-?\d+?\b" },

				},
				WordTokens = new()
				{

				},
			},
			new("Xml")
			{
				FoldingPairs = new()
				{

				},
				RegexTokens = new()
				{
					{ Token.Command, /*language=regex*/ @"</?.+?/?>" },
					{ Token.String, /*language=regex*/"\\\".*?\\\"|'.*?'" },
					{ Token.Symbol, /*language=regex*/ @"[:=<>,.!?&%+\|\-*\/\^~;´`]" },
					{ Token.Bracket, /*language=regex*/ @"[\[\]\(\)\{\}]" },
					{ Token.Number, /*language=regex*/ @"0[xX][0-9a-fA-F]*|-?\d*\.\d+([eE][\-+]?\d+)?|-?\d+?" },
				},
				WordTokens = new()
				{

				},
			},
			new("Text")
			{
				FoldingPairs = new()
				{

				},
				RegexTokens = new()
				{

				},
				WordTokens = new()
				{

				},
			},
		};

		public Language EditorLanguage { get => Get(LanguageList.First(x => x.Name == Language)); set { Set(value); } }
		public int FontSize { get => Get(20); set => Set(value); }
		public bool IsFoldingEnabled { get => Get(true); set => Set(value); }
		public bool IsWrappingEnabled { get => Get(false); set => Set(value); }
		public string Language { get => Get("Lua"); set { Set(value); EditorLanguage = LanguageList.First(x => x.Name == value); } }
		public string[] LanguageOptions => LanguageList.Select(x => x.Name).ToArray();
		public string LastSavedText { get => Get(""); set { Set(value); UnsavedChanges = value != Text; } }
		public string Log { get => Get(""); set => Set(value); }
		public ElementTheme RequestedTheme { get => Get(ElementTheme.Default); set => Set(value); }
		public bool ShowControlCharacters { get => Get(false); set => Set(value); }
		public bool ShowHorizontalTicks { get => Get(false); set => Set(value); }
		public bool ShowLineMarkers { get => Get(true); set => Set(value); }
		public bool ShowLineNumbers { get => Get(true); set => Set(value); }
		public bool ShowScrollbarMarkers { get => Get(true); set => Set(value); }
		public int TabLength { get => Get(2); set => Set(value); }
		public string Text { get => Get(""); set { Set(value); UnsavedChanges = value != LastSavedText; } }
		public string Theme { get => Get("Default"); set { Set(value); RequestedTheme = (ElementTheme)Enum.Parse(typeof(ElementTheme), value); } }
		public string Font { get => Get("Consolas"); set => Set(value); }

		public IndentGuide ShowIndentGuides { get => Get(IndentGuide.None); set => Set(value); }
		public string ShowIndentGuidesOption { get => Get("None"); set { Set(value); ShowIndentGuides = (IndentGuide)Enum.Parse(typeof(IndentGuide), value); } }
		public string[] ShowIndentGuidesOptions => Enum.GetNames<IndentGuide>();
		public string[] ThemeOptions => Enum.GetNames<ElementTheme>();
		public string[] FontOptions => new[] { "Consolas", "Courier New", "Lucida Sans Typewriter", "Cascadia Code", "Cascadia Mono", "Fira Code", "JetBrains Mono" };
		public bool UnsavedChanges { get => Get(false); set => Set(value); }

		public ObservableCollection<TokenDefinition> TokenColorDefinitions
		{
			get => Get(new ObservableCollection<TokenDefinition>(EditorOptions.TokenColors.Select(x => new TokenDefinition() { Token = x.Key, Color = x.Value })));
			set => Set(value);
		}
	}
}