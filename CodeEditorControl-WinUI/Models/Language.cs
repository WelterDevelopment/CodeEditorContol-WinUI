using System.Collections.Generic;
using Windows.UI;

namespace CodeEditorControl_WinUI;
public class Language
{
	public Language(string language)
	{
		Name = language;
	}

	public List<SyntaxFolding> FoldingPairs { get; set; }
	public List<NestedLanguage> NestedLanguages { get; set; }
	public string Name { get; set; }

	public char[] CommandTriggerCharacters { get; set; } = new char[] { };
	public char[] OptionsTriggerCharacters { get; set; } = new char[] { };

	public char[] EscapeSymbols { get; set; } = new char[] { };
	public Dictionary<Token, string> RegexTokens { get; set; }
	public Dictionary<Token, string[]> WordTokens { get; set; }
	public Dictionary<char, char> AutoClosingPairs { get; set; } = new();
	public List<Suggestion> Commands { get; set; }
	public List<string> WordSelectionDefinitions { get; set; } = new() { /*language=regex*/ @"\b\w+?\b", };

	public string LineComment { get; set; }

	public bool EnableIntelliSense { get; set; } = false;
}

public class NestedLanguage
{
	public string InnerLanguage { get; set; }
	public string RegexEnd { get; set; }
	public string RegexStart { get; set; }
}
public static class EditorOptions
{
	public static Dictionary<Token, Color> TokenColors = new()
		{
			{ Token.Normal, Color.FromArgb(255, 220, 220, 220) },
			{ Token.Command, Color.FromArgb(255, 50, 130, 210) },
			{ Token.Function, Color.FromArgb(255, 200, 120, 220) },
			{ Token.Keyword, Color.FromArgb(255, 50, 130, 210) },
			{ Token.Environment, Color.FromArgb(255, 50, 190, 150) },
			{ Token.Comment, Color.FromArgb(255, 40, 190, 90) },
			{ Token.Key, Color.FromArgb(255, 150, 120, 200) },
			{ Token.Bracket, Color.FromArgb(255, 100, 140, 220) },
			{ Token.Reference, Color.FromArgb(255, 180, 140, 40) },
			{ Token.Math, Color.FromArgb(255, 220, 160, 60) },
			{ Token.Symbol, Color.FromArgb(255, 140, 200, 240) },
			{ Token.Style, Color.FromArgb(255, 220, 130, 100) },
			{ Token.String, Color.FromArgb(255, 220, 130, 100) },
			{ Token.Special, Color.FromArgb(255, 50, 190, 150) },
			{ Token.Number, Color.FromArgb(255, 180, 220, 180) },
			{ Token.Array, Color.FromArgb(255, 200, 100, 80) },
			{ Token.Primitive, Color.FromArgb(255, 230, 120, 100) },
		};
}
public class TokenDefinition : Bindable
{
	public Token Token { get => Get(Token.Normal); set => Set(value); }
	public Color Color
	{
		get => Get(Color.FromArgb(255, 220, 220, 220));
		set
		{
			Set(value);
			try
			{
				EditorOptions.TokenColors[Token] = value;
				CodeWriter.Current?.RedrawText();
			}
			catch { }
		}
	}
}
public class SyntaxFolding
{
	public string RegexEnd { get; set; }
	public string RegexStart { get; set; }
	public int MatchingGroup { get; set; } = -1;
	public List<string> FoldingIgnoreWords { get; set; }
}

public static class Languages
{

	public static List<Language> LanguageList = new()
		{
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
}