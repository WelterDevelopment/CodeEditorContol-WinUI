using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeEditorControl_WinUI;

public partial class CodeWriter : UserControl, INotifyPropertyChanged
{
	class CommandAtPosition
	{
		public IntelliSense Command { get; set; }
		public Range CommandRange { get; set; }
		public List<Range> ArgumentsRanges { get; set; }
	}
	private List<Suggestion> Commands
	{
		get => Get(new List<Suggestion>() {
					new IntelliSense(@"\foo"){ IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
					new IntelliSense(@"\bar"){ IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
					new IntelliSense(@"\foobar"){ IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
			}); set => Set(value);
	}
	private Place SuggestionStart = new Place();
	private int SuggestionIndex { get => Get(-1); set { Set(value); if (value == -1) SelectedSuggestion = null; else if (Suggestions?.Count > value) { SelectedSuggestion = Suggestions[value]; Lbx_Suggestions.ScrollIntoView(SelectedSuggestion); } } }
	private Suggestion SelectedSuggestion { get => Get<Suggestion>(); set => Set(value); }

	private List<Suggestion> AllOptions { get => Get<List<Suggestion>>(); set => Set(value); }
	private List<Suggestion> AllSuggestions { get => Get(Commands); set => Set(value); }
	private List<Suggestion> Suggestions { get => Get(Commands); set => Set(value); }
	private List<Parameter> Options	{	get => Get(new List<Parameter>()); set => Set(value);	}

	public void UpdateSuggestions()
	{
		AllSuggestions = Language.Commands;
		Suggestions = Language.Commands;
	}
	private void InsertSuggestion()
	{
		TextControl.Focus(FocusState.Keyboard);

		Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Remove(SuggestionStart.iChar, CursorPlace.iChar - SuggestionStart.iChar));

		EditActionHistory.Remove(EditActionHistory.LastOrDefault());
		TextAction_Paste(Suggestions[SuggestionIndex].Name + Suggestions[SuggestionIndex].Snippet + Suggestions[SuggestionIndex].Options, SuggestionStart, false);

		int iCharStart = 0;
		int iCharEnd = 0;
		if (Suggestions[SuggestionIndex].IntelliSenseType == IntelliSenseType.Argument)
		{
			iCharStart = SuggestionStart.iChar + Suggestions[SuggestionIndex].Name.Length + Suggestions[SuggestionIndex].Snippet.Length;
			iCharEnd = iCharStart + Suggestions[SuggestionIndex].Options.Length;
		}
		else
		{
			iCharStart = SuggestionStart.iChar + Suggestions[SuggestionIndex].Name.Length;
			iCharEnd = iCharStart;
		}
		Selection = new(new Place(iCharStart, CursorPlace.iLine), new Place(iCharEnd, CursorPlace.iLine));
		IsSuggesting = false;
	}

	private void FilterSuggestions(int offset = 0)
	{
		if (IsSuggesting)
		{
			try
			{
				string searchString = Lines[SuggestionStart.iLine].LineText.Substring(SuggestionStart.iChar, CursorPlace.iChar - SuggestionStart.iChar);
				List<Suggestion> matchingSuggestions;
				if (!IsSuggestingOptions)
				{
					matchingSuggestions = AllSuggestions
									.Where(m => m.Name.Contains(searchString))
									.OrderBy(m => m.Name)
									.ToList();
				}
				else
				{
					matchingSuggestions = AllOptions
								.Where(m => m.Name.Contains(searchString))
								.OrderBy(m => m.Name)
								.ToList();
				}
				if (matchingSuggestions.Count > 0)
				{
					Suggestions = matchingSuggestions;
					SuggestionIndex = 0;
				}
				else
					SuggestionIndex = -1;
			}
			catch
			{

			}
		}
	}

	private CommandAtPosition GetCommandAtPosition(Place place)
	{
		CommandAtPosition commandAtPosition = new CommandAtPosition();
		MatchCollection commandsInLine = Regex.Matches(Lines[place.iLine].LineText, @"(\\.+?\b)(\[(?>\[(?<c>)|[^\[\]]+|\](?<-c>))*(?(c)(?!))\])*");
		if (commandsInLine.Any() && AllSuggestions != null)
		{
			foreach (Match command in commandsInLine)
			{
				if (command.Success && place.iChar >= command.Index && place.iChar <= command.Index + command.Length)
				{
					var commandname = command.Groups[1];
					commandAtPosition.CommandRange = new(new(commandname.Index, place.iLine), new(command.Index + command.Length, place.iLine));
					commandAtPosition.Command = AllSuggestions.FirstOrDefault(x => x.Name == commandname.Value) as IntelliSense;
					if (command.Groups.Count > 2)
					{
						commandAtPosition.ArgumentsRanges = new();
						for (int group = 2; group < command.Groups.Count; group++)
						{
							var argument = command.Groups[group];
							commandAtPosition.ArgumentsRanges.Add(new(new(argument.Index, place.iLine), new(argument.Index + argument.Length, place.iLine)));
						}
					}
					return commandAtPosition;
				}
			}
		}
		return commandAtPosition;
	}

	private IntelliSense GetCommandFromPlace(Place place)
	{
		Place start = place;
		Place end = new Place(start.iChar + 1, start.iLine);
		var matches = Regex.Matches(Lines[start.iLine].LineText, @"(\\.+?)(\s*?)(\[)");
		string command = "";
		foreach (Match match in matches)
		{
			command = match?.Groups[1]?.Value;
		}
		if (!string.IsNullOrEmpty(command))
			return AllSuggestions.FirstOrDefault(x => x.Name == command) as IntelliSense;
		else return null;
	}

	private bool IsInsideBrackets(Place place)
	{
		List<BracketPair> pairs = new();

		for (int i = 0; i < Lines[place.iLine].LineText.Length; i++)
		{
			if (Lines[place.iLine].LineText[i] == '[')
			{
				int open = i;
				int close = findClosingParen(Lines[place.iLine].LineText.ToCharArray(), i);
				pairs.Add(new BracketPair(new Place(open, place.iLine), new Place(close, place.iLine)));
			}
		}

		return pairs.Any(x => x.iClose >= place && x.iOpen < place);
	}

	private int findClosingParen(char[] text, int openPos)
	{
		int closePos = openPos;
		int counter = 1;
		while (counter > 0)
		{
			if (closePos == text.Length - 1)
			{
				return ++closePos;
			}
			char c = text[++closePos];
			if (c == '[')
			{
				counter++;
			}
			else if (c == ']')
			{
				counter--;
			}
		}
		return closePos;
	}
}