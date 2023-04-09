using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeEditorControl_WinUI;

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
	public Token T { get => Get(Token.Normal); set => Set(value); }
}

/// <summary> Currently unused. ToDo: Render whole words at once </summary>
public class CharGroup : CharElement
{
	public CharGroup(Char[] chars)
	{
		C = chars;
	}

	public new Char[] C { get => Get(new Char[] { }); set => Set(value); }
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
		int lastChar = Count - 1;
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
					int maxchar = iVisibleChars - 1;
					//if (iChar + indents * (TabLength - 1) - iCharOffset < maxchar)
					//{
					//	x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
					//}
					//else
					//{
					int iWrappingEndPosition = (linewraps + 1) * maxchar - (WrapIndent * linewraps) - (indents * (TabLength - 1) * (linewraps + 1));

					iWrappingEndPosition = Math.Min(iWrappingEndPosition, lastChar);

					if (iChar > 0 && iChar <= iWrappingEndPosition)
						iWrappingChar++;

					if (iChar == iWrappingEndPosition)
					{
						iWrappingChar = WrapIndent;
						linewraps++;
						WrappedLines.Add(new(Chars.GetRange(iLastWrappingPosition, iChar - iLastWrappingPosition + 1)));
						iLastWrappingPosition = iChar + 1;
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
	public Language Language { get => Get<Language>(); set { Set(value); SetLineText(LineText); } }
	public int LineNumber { get => Get(0); set => Set(value); }

	public void Save() { lastsavedtext = LineText; IsUnsaved = false; }

	public string LineText
	{
		get => Get("");
		set
		{
			IsUnsaved = value != lastsavedtext;

			Set(value);

		}
	}

	public void SetLineText(string value)
	{
		LineText = value;

		//await Task.Run(() =>
		//{
		Chars = FormattedText(value);
		//IsFoldStart = FoldableStart(value);
		//IsFoldInnerEnd = FoldableEnd(value);
		//IsFoldInner = !IsFoldStart && !IsFoldInnerEnd;
		//});
	}

	public void AddToLineText(string value)
	{
		LineText += value;

		//Task.Run(() =>
		//{
		Chars = FormattedText(LineText);
		//IsFoldStart = FoldableStart(value);
		//IsFoldInnerEnd = FoldableEnd(value);
		//IsFoldInner = !IsFoldStart && !IsFoldInnerEnd;
		//}).Wait();

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

		if (!string.IsNullOrEmpty(Language.LineComment))
		{
			int commentIndex = text.IndexOf(Language.LineComment);
			if (commentIndex > -1)
			{
				if (commentIndex > 0 && Language.EscapeSymbols.Contains(groups[commentIndex - 1].C))
				{

				}
				else
					for (int i = commentIndex; i < text.Count(); i++)
					{
						groups[i].T = Token.Comment;
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
