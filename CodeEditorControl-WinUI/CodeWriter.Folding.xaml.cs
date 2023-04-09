using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeEditorControl_WinUI;

public partial class CodeWriter : UserControl, INotifyPropertyChanged
{
	List<Folding> foldings = new List<Folding>();
	private void updateFoldingPairs()
	{
		try
		{
			foldings.Clear();
		if (Language.FoldingPairs != null)
		{
			foreach (var line in Lines.ToList())
			{
				if (line != null && line.Language.FoldingPairs != null)
				{
					foreach (SyntaxFolding syntaxFolding in line.Language.FoldingPairs)
					{
						var startmatch = Regex.Matches(line.LineText, syntaxFolding.RegexStart);
						foreach (Match match in startmatch)
						{
							if (match.Success)
								if (syntaxFolding.FoldingIgnoreWords == null | (syntaxFolding.FoldingIgnoreWords != null && !syntaxFolding.FoldingIgnoreWords.Contains(match.Groups[2].Value)))
									if (syntaxFolding.MatchingGroup > 0)
										foldings.Add(new() { Name = match.Groups[2].Value, StartLine = line.iLine });
									else
										foldings.Add(new() { Name = match.Value, StartLine = line.iLine });
						}

						var endmatch = Regex.Matches(line.LineText, syntaxFolding.RegexEnd);
						foreach (Match match in endmatch)
						{
							if (match.Success)
								if (syntaxFolding.FoldingIgnoreWords == null | (syntaxFolding.FoldingIgnoreWords != null && !syntaxFolding.FoldingIgnoreWords.Contains(match.Groups[2].Value)))
								{
									Folding matchingfolding;
									if (syntaxFolding.MatchingGroup > 0)
										matchingfolding = foldings.LastOrDefault(x => x.Name == match.Groups[syntaxFolding.MatchingGroup].Value);
									else
										matchingfolding = foldings.LastOrDefault(x => x.Endline == -1);

									if (matchingfolding != null)
									{
										matchingfolding.Endline = line.iLine;
									}
								}
						}
					}
				}
			}
			foldings.RemoveAll(x => x.Endline == -1); // remove anything unmatched
																																													//InfoMessage?.Invoke(this, string.Join("", foldings.Select(x => "\n" + x.Name + ": " + x.StartLine + "->" + x.Endline)));
		}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new(ex));
		}
	}

}