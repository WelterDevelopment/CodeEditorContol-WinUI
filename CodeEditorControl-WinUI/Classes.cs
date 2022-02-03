using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Windows.Foundation;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CodeEditorControl_WinUI
{
	public enum EditActionType
	{
		Delete, Paste, Add, Remove
	}

	public enum IntelliSenseType
	{
		Command, Argument,
	}

	public enum LexerState
	{
		Normal, Comment, String
	}

	public enum SelectionType
	{
		Selection, SearchMatch, WordLight
	}

	public enum SyntaxErrorType
	{
		None, Error, Warning, Message
	}

	public enum Token
	{
		Normal, Environment, Command, Function, Keyword, Primitive, Definition, String, Comment, Dimension, Text, Reference, Key, Value, Number, Bracket, Style, Array, Symbol,
		Math, Special
	}

	public enum VisibleState
	{
		Visible, StartOfHiddenBlock, Hidden
	}

	public static class Extensions
	{
		public static Vector2 Center(this Rect rect)
		{
			return new Vector2((float)rect.X + (float)rect.Width / 2, (float)rect.Y + (float)rect.Height / 2);
		}

		public static Color ChangeColorBrightness(this Color color, float correctionFactor)
		{
			float red = color.R;
			float green = color.G;
			float blue = color.B;

			if (correctionFactor < 0)
			{
				correctionFactor = 1 + correctionFactor;
				red *= correctionFactor;
				green *= correctionFactor;
				blue *= correctionFactor;
			}
			else
			{
				red = (255 - red) * correctionFactor + red;
				green = (255 - green) * correctionFactor + green;
				blue = (255 - blue) * correctionFactor + blue;
			}

			return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
		}

		public static Color InvertColorBrightness(this Color color)
		{
			// ToDo: Come up with some fancy way of producing perfect colors for the light theme
			float red = color.R;
			float green = color.G;
			float blue = color.B;

			float lumi = (0.33f * red) + (0.33f * green) + (0.33f * blue);

			red = 255 - lumi + 0.6f * (red - lumi);
			green = 255 - lumi + 0.35f * (green - lumi);
			blue = 255 - lumi + 0.4f * (blue - lumi);

			return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
		}

		public static System.Drawing.Point ToDrawingPoint(this Windows.Foundation.Point point)
		{
			return new System.Drawing.Point((int)point.X, (int)point.Y);
		}

		public static Windows.Foundation.Point ToFoundationPoint(this System.Drawing.Point point)
		{
			return new Windows.Foundation.Point(point.X, point.Y);
		}

		public static Windows.UI.Color ToUIColor(this System.Drawing.Color color)
		{
			return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		public static Vector2 ToVector2(this System.Drawing.Point point)
		{
			return new Vector2((float)point.X, (float)point.Y);
		}
	}

	public static class Languages
	{

		public static Language Lua = new("Lua")
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
				{ Token.Comment, /*language=regex*/ "\\\"[^\\\"]*\\\" | --.*?\\\n" },
			},
			WordTokens = new()
			{
				{ Token.Keyword, new string[] { "local", "true", "false", "in", "else", "not", "or", "and", "then", "nil", "end", "do", "repeat", "goto", "until", "return", "break" } },
				{ Token.Environment, new string[] { "function", "end", "if", "elseif", "else", "while", "for", } },
				{ Token.Function, new string[] { "#", "assert", "collectgarbage", "dofile", "_G", "getfenv", "ipairs", "load", "loadstring", "pairs", "pcall", "print", "rawequal", "rawget", "rawset", "select", "setfenv", "_VERSION", "xpcall", "module", "require", "tostring", "tonumber", "type", "rawset", "setmetatable", "getmetatable", "error", "unpack", "next", } }
			},
		};
	}


	public class TokenDefinition : Bindable
	{
		public Token Token { get => Get(Token.Normal); set => Set(value); }
		public Color Color
		{
			get => Get(Color.FromArgb(255, 220, 220, 220)); set
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

	public class Bindable : INotifyPropertyChanged
	{
		private Dictionary<string, object> _properties = new Dictionary<string, object>();

		public event PropertyChangedEventHandler PropertyChanged;

		protected T Get<T>(T defaultVal = default, [CallerMemberName] string name = null)
		{
			if (!_properties.TryGetValue(name, out object value))
			{
				value = _properties[name] = defaultVal;
			}
			return (T)value;
		}

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected void Set<T>(T value, [CallerMemberName] string name = null)
		{
			if (Equals(value, Get<T>(value, name)))
				return;
			_properties[name] = value;
			OnPropertyChanged(name);
		}
	}

	public class BracketPair
	{
		public BracketPair()
		{
		}

		public BracketPair(Place open, Place close)
		{
			iOpen = open;
			iClose = close;
		}

		public Place iClose { get; set; } = new Place();
		public Place iOpen { get; set; } = new Place();
	}

	public class Char : CharElement
	{
		public Char(char c)
		{
			C = c;
		}
	}

	public class CharElement : Bindable
	{
		public char C { get => Get(' '); set => Set(value); }

		//public Color ForeGround { get => Get(Colors.White); set { Set(value); } }
		public Token T { get => Get(Token.Normal); set => Set(value); }
	}

	public class CharGroup : CharElement
	{
		public CharGroup(Char[] chars)
		{
			C = chars;
		}

		public new Char[] C { get => Get(new Char[] { }); set => Set(value); }
	}

	public class CodeWriterOptions : Bindable
	{
	}

	public class EditAction
	{
		public EditActionType EditActionType { get; set; }
		public string TextState { get; set; }
		public string TextInvolved { get; set; }
		public Range Selection { get; set; }
	}

	public class Folding : Bindable
	{
		public int Endline { get => Get(0); set => Set(value); }
		public int StartLine { get => Get(0); set => Set(value); }
	}

	public class HighlightRange
	{
		public Place End { get; set; }
		public Place Start { get; set; }
	}

	public class Suggestion : Bindable
	{
		public string Name { get; set; }

		public Token Token { get; set; } = Token.Normal;
		public IntelliSenseType IntelliSenseType { get; set; } = IntelliSenseType.Command;
		public string Snippet { get; set; }
		public string Description { get; set; }
	}

	public class IntelliSense : Suggestion
	{
		public IntelliSense(string text)
		{
			Name = text;
			Token = Token.Command;
		}

		public List<Argument> ArgumentsList { get; set; } = new();
	}


	public class Argument : Suggestion
	{
		public int Number { get; set; }

		public bool IsSelected { get => Get(false); set => Set(value); }

		public string Optional { get; set; }

		public string Delimiters { get; set; }

		public string List { get; set; }

		public List<Parameter> Parameters { get; set; }
	}


	public class Parameter : Suggestion
	{
		public List<Constant> Constant { get; set; }


	}
	public class Constant
	{
		public string Type { get; set; }

		public string Default { get; set; }

		public string Value { get; set; }
	}

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
		public Dictionary<Token, string> RegexTokens { get; set; }
		public Dictionary<Token, string[]> WordTokens { get; set; }
		public Dictionary<char, char> AutoClosingPairs { get; set; } = new();
		public List<Suggestion> Commands { get; set; }
		public List<string> WordSelectionDefinitions { get; set; } = new() { /*language=regex*/ @"\b\w+?\b", };

		public string LineComment { get; set; }
	}

	public class Line : Bindable
	{
		public VisibleState VisibleState = VisibleState.Visible;

		private string lastsavedtext = null;

		public Line(Language language = null)
		{
			if (language != null)
				Language = language;
		}

		public List<Char> Chars { get => Get(new List<Char>()); set => Set(value); }

		public List<List<Char>> WrappedLines { get => Get(new List<List<Char>>()); set => Set(value); }

		public int Count
		{
			get { return Chars.Count; }
		}

		public Folding Folding { get => Get(new Folding()); set => Set(value); }

		public string FoldingEndMarker { get; set; }

		public string FoldingStartMarker { get; set; }

		public int iLine { get => LineNumber - 1; }

		public int Indents { get { return LineText != null ? LineText.Count(x => x == '\t') : 0; } }

		public int GetLineWraps(int maxchar, int tablength, int wrappingindent)
		{

			int linewraps = 0;
			int indents = Indents;
			int iWrappingChar = 0;
			List<Char> wrappedLine = new List<Char>();

			for (int iChar = 0; iChar < Count - 1; iChar++)
			{
				int iWrappingEndPosition = (linewraps + 1) * maxchar - (wrappingindent * linewraps) - (indents * (tablength - 1) * (linewraps + 1));

				if (iChar > 0 && iChar < iWrappingEndPosition)
					iWrappingChar++;

				if (iChar == iWrappingEndPosition)
				{
					iWrappingChar = wrappingindent;
					linewraps++;
				}
			}

			return linewraps;
		}

		public void CalculateWrappedLines(int iVisibleChars, int TabLength = 2, int WrapIndent = 3)
		{
			WrappedLines.Clear();
			int lastChar = Count-1;
			int indents = 0;
			int linewraps = 0;
			int iWrappingChar = 0;
			int iLastWrappingPosition = 0;
			if (lastChar == -1)
			{
				WrappedLines.Add(new(Chars));
			}
			else
			for (int iChar = 0; iChar <= lastChar; iChar++)
			{
				Char c = this[iChar];

				if (c.C == '\t')
				{
					//x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
					indents += 1;
				}
				else if (iChar >= indents * (TabLength - 1))
				{
					int maxchar = iVisibleChars-1;
					//if (iChar + indents * (TabLength - 1) - iCharOffset < maxchar)
					//{
					//	x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
					//}
					//else
					//{
					int iWrappingEndPosition = (linewraps + 1) * maxchar - (WrapIndent * linewraps) - (indents * (TabLength - 1) * (linewraps +1));

					iWrappingEndPosition = Math.Min(iWrappingEndPosition,lastChar);

					if (iChar > 0 && iChar <= iWrappingEndPosition)
						iWrappingChar++;

					if (iChar == iWrappingEndPosition)
					{
						iWrappingChar = WrapIndent;
						linewraps++;
						WrappedLines.Add(new(Chars.GetRange(iLastWrappingPosition, iChar - iLastWrappingPosition+1)));
						iLastWrappingPosition = iChar+1;
						//	args.DrawingSession.FillRectangle(0, y, Width_Left - Width_TextIndent, CharHeight, Color_LeftBackground);
						//wrapindent = 1;
					}


				}
			}
		}

		public bool IsFoldEnd { get => Get(false); set => Set(value); }
		public bool IsFoldInner { get => Get(false); set => Set(value); }
		public bool IsFoldInnerEnd { get => Get(false); set => Set(value); }
		public bool IsFoldStart { get => Get(false); set => Set(value); }
		public bool IsUnsaved { get => Get(false); set { Set(value); } }
		public Language Language { get => Get<Language>(); set { Set(value); Chars = FormattedText(LineText); } }
		public int LineNumber { get => Get(0); set => Set(value); }

		public void Save() { lastsavedtext = LineText; IsUnsaved = false; }

		public string LineText
		{
			get => Get("");
			set
			{
				IsUnsaved = value != lastsavedtext;

				Set(value);

				Chars = FormattedText(value);
				IsFoldStart = FoldableStart(value);
				IsFoldInnerEnd = FoldableEnd(value);
				IsFoldInner = !IsFoldStart && !IsFoldInnerEnd;
			}
		}

		public int WordWrapStringsCount { get; internal set; }

		public Char this[int index]
		{
			get
			{
				return Chars[index];
			}
			set
			{
				Chars[index] = value;
			}
		}

		public void Add(Char item)
		{
			Chars.Add(item);
		}

		public virtual void AddRange(IEnumerable<Char> collection)
		{
			//Chars.AddRange(collection);
		}

		public void Clear()
		{
			Chars.Clear();
		}

		public bool Contains(Char item)
		{
			return Chars.Contains(item);
		}

		public void CopyTo(Char[] array, int arrayIndex)
		{
			Chars.CopyTo(array, arrayIndex);
		}

		public List<Char> FormattedText(string text)
		{
			List<Char> groups = new();

			groups = text.Select(x => new Char(x)).ToList();

			if (Language.RegexTokens != null)
				foreach (var token in Language.RegexTokens)
				{
					MatchCollection mc = Regex.Matches(text, token.Value);
					foreach (Match match in mc)
					{
						for (int i = match.Index; i < match.Index + match.Length; i++)
						{
							groups[i].T = token.Key;
						}
					}
				}

			if (Language.WordTokens != null)
				foreach (var token in Language.WordTokens)
				{
					var list = token.Value.ToList();
					for (int i = 0; i < list.Count; i++)
					{
						list[i] = list[i].Replace(@"\", @"\\");
					}
					string pattern;
					if (Language.Name == "ConTeXt")
						pattern = string.Join(@"\b|", list) + @"\b";
					else
						pattern = @"\b" + string.Join(@"\b|\b", list) + @"\b";
					MatchCollection mc = Regex.Matches(text, pattern);
					foreach (Match match in mc)
					{
						for (int i = match.Index; i < match.Index + match.Length; i++)
						{
							groups[i].T = token.Key;
						}
					}
				}

			return new(groups);
		}

		public IEnumerator<Char> GetEnumerator()
		{
			return Chars.GetEnumerator();
		}

		public int IndexOf(Char item)
		{
			return Chars.IndexOf(item);
		}

		public void Insert(int index, Char item)
		{
			Chars.Insert(index, item);
		}

		public bool Remove(Char item)
		{
			return Chars.Remove(item);
		}

		public void RemoveAt(int index)
		{
			Chars.RemoveAt(index);
		}

		internal int GetWordWrapStringFinishPosition(int v, Line line)
		{
			return 0;
		}

		internal int GetWordWrapStringIndex(int iChar)
		{
			return 0;
		}

		internal int GetWordWrapStringStartPosition(object v)
		{
			return 0;
		}

		private bool FoldableEnd(string text)
		{
			if (Language.FoldingPairs != null)
				foreach (SyntaxFolding syntaxFolding in Language.FoldingPairs)
				{
					var match = Regex.Match(text, syntaxFolding.RegexEnd);
					if (match.Success)
					{
						return true;
					}
				}
			return false;
		}

		private bool FoldableStart(string text)
		{
			if (Language.FoldingPairs != null)
				foreach (SyntaxFolding syntaxFolding in Language.FoldingPairs)
				{
					var match = Regex.Match(text, syntaxFolding.RegexStart);
					if (match.Success)
					{
						return true;
					}
				}
			return false;
		}
	}

	public class Place : IEquatable<Place>
	{
		public int iChar = 0;
		public int iLine = 0;

		public Place()
		{
		}

		public Place(Place oldplace)
		{
			this.iChar = oldplace.iChar;
			this.iLine = oldplace.iLine;
		}

		public Place(int iChar, int iLine)
		{
			this.iChar = iChar;
			this.iLine = iLine;
		}

		public static Place Empty
		{
			get { return new Place(); }
		}

		public static bool operator !=(Place p1, Place p2)
		{
			return !p1.Equals(p2);
		}

		public static Place operator +(Place p1, Place p2)
		{
			return new Place(p1.iChar + p2.iChar, p1.iLine + p2.iLine);
		}

		public static Place operator +(Place p1, int c2)
		{
			return new Place(p1.iChar + c2, p1.iLine);
		}
		public static Place operator -(Place p1, int c2)
		{
			return new Place(p1.iChar - c2, p1.iLine);
		}

		public static bool operator <(Place p1, Place p2)
		{
			if (p1.iLine < p2.iLine) return true;
			if (p1.iLine > p2.iLine) return false;
			if (p1.iChar < p2.iChar) return true;
			return false;
		}

		public static bool operator <=(Place p1, Place p2)
		{
			if (p1.Equals(p2)) return true;
			if (p1.iLine < p2.iLine) return true;
			if (p1.iLine > p2.iLine) return false;
			if (p1.iChar < p2.iChar) return true;
			return false;
		}

		public static bool operator ==(Place p1, Place p2)
		{
			return p1.Equals(p2);
		}

		public static bool operator >(Place p1, Place p2)
		{
			if (p1.iLine > p2.iLine) return true;
			if (p1.iLine < p2.iLine) return false;
			if (p1.iChar > p2.iChar) return true;
			return false;
		}

		public static bool operator >=(Place p1, Place p2)
		{
			if (p1.Equals(p2)) return true;
			if (p1.iLine > p2.iLine) return true;
			if (p1.iLine < p2.iLine) return false;
			if (p1.iChar > p2.iChar) return true;
			return false;
		}

		public bool Equals(Place other)
		{
			return iChar == other.iChar && iLine == other.iLine;
		}

		public override bool Equals(object obj)
		{
			return (obj is Place) && Equals((Place)obj);
		}

		public override int GetHashCode()
		{
			return iChar.GetHashCode() ^ iLine.GetHashCode();
		}

		public void Offset(int dx, int dy)
		{
			iChar += dx;
			iLine += dy;
		}

		public override string ToString()
		{
			return "(" + (iLine + 1) + "," + (iChar + 1) + ")";
		}
	}

	public class RelayCommand : ICommand
	{
		private readonly Func<bool> _canExecute;
		private readonly Action _execute;

		public RelayCommand(Action execute)
						: this(execute, null)
		{
		}

		public RelayCommand(Action execute, Func<bool> canExecute)
		{
			_execute = execute ?? throw new ArgumentNullException("execute");
			_canExecute = canExecute;
		}

		public event EventHandler CanExecuteChanged;

		public bool CanExecute(object parameter)
		{
			return _canExecute == null ? true : _canExecute();
		}

		public void Execute(object parameter)
		{
			_execute();
		}

		public void RaiseCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public class SearchMatch
	{
		public int iChar { get; set; }
		public int iLine { get; set; }
		public string Match { get; set; }
	}

	public class Range : Bindable
	{
		public Range(Range range)
		{
			Start = range.Start;
			End = range.End;
		}
		public Range(Place place)
		{
			Start = place ?? new Place();
			End = place ?? new Place();
		}

		public Range(Place start, Place end)
		{
			Start = start;
			End = end;
		}

		public Range()
		{
		}

		public static Range operator +(Range p1, int c2)
		{
			return new Range(p1.Start + c2, p1.End + c2);
		}
		public static Range operator -(Range p1, int c2)
		{
			return new Range(p1.Start - c2, p1.End - c2);
		}

		public Place End { get => Get(new Place()); set => Set(value); }
		public Place Start { get => Get(new Place()); set => Set(value); }

		public Place VisualEnd { get => End > Start ? new(End) : new(Start); }
		public Place VisualStart { get => End > Start ? new(Start) : new(End); }

		public override string ToString() => Start.ToString() + " -> " + End.ToString();
	}

	public class SyntaxError
	{
		public string Description { get; set; } = "";
		public int iChar { get; set; } = 0;
		public int iLine { get; set; } = 0;
		public SyntaxErrorType SyntaxErrorType { get; set; } = SyntaxErrorType.None;
		public string Title { get; set; } = "";
	}

	public class NestedLanguage
	{
		public string InnerLanguage { get; set; }
		public string RegexEnd { get; set; }
		public string RegexStart { get; set; }
	}

	public class SyntaxFolding
	{
		public string RegexEnd { get; set; }
		public string RegexStart { get; set; }
	}

	public class WidthToThickness : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string culture)
		{
			double offset = (double)value;
			return new Thickness(0, offset, 0, offset);
		}

		public object ConvertBack(object value, Type targetType, object parameter, string culture)
		{
			return 0;
		}
	}

	public class TokenToColor : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string culture)
		{
			if (value is Token token)
				return EditorOptions.TokenColors[token];
			else return EditorOptions.TokenColors[Token.Normal];
		}

		public object ConvertBack(object value, Type targetType, object parameter, string culture)
		{
			return 0;
		}
	}
	public class FocusToVisibility : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string culture)
		{
			FocusState state = (FocusState)value;
			return state != FocusState.Unfocused ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string culture)
		{
			return 0;
		}
	}
	public class ArgumentsToString : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string culture)
		{
			string argstring = "";
			List<Argument> list = (List<Argument>)value;
			foreach (var item in list)
			{
				string delstart = "";
				string delend = "";
				switch (item.Delimiters)
				{
					case "parentheses ": delstart = "("; delend = ")"; break;
					case "braces": delstart = "{"; delend = "}"; break;
					case "anglebrackets": delstart = "<"; delend = ">"; break;
					case "none": delstart = ""; delend = ""; break;
					case "brackets": delstart = "["; delend = "]"; break;
					default: delstart = "["; delend = "]"; break;
				}
				argstring += " " + delstart + "..." + delend;
			}
			return argstring;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string culture)
		{
			return null;
		}
	}

	public class SuggestionTemplateSelector : DataTemplateSelector
	{
		public DataTemplate IntelliSenseTemplate { get; set; }
		public DataTemplate ArgumentTemplate { get; set; }

		protected override DataTemplate SelectTemplateCore(object item, DependencyObject dependency)
		{
			if (item is IntelliSense)
			{
				return IntelliSenseTemplate;
			}
			else if (item is Parameter)
			{
				return ArgumentTemplate;
			}
			else
				return null;
		}

	}
}