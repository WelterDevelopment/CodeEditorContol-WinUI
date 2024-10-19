using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;

namespace CodeEditorControl_WinUI;

public partial class CodeWriter : UserControl, INotifyPropertyChanged
{
	public bool CanUndo { get => Get(false); set => Set(value); }
	public bool CanToggleComment { get => Get(false); set => Set(value); }
	public bool CanRedo { get => Get(false); set => Set(value); }

	public ObservableCollection<Place> CursorPlaceHistory = new();
	public ObservableCollection<EditAction> EditActionHistory
	{
		get => (ObservableCollection<EditAction>)GetValue(EditActionHistoryProperty);
		set
		{
			SetValue(EditActionHistoryProperty, value);
			value.CollectionChanged += EditActionHistory_CollectionChanged;
		}
	}
	public ObservableCollection<EditAction> InvertedEditActionHistory { get => Get(new ObservableCollection<EditAction>()); set => Set(value); }

	public void Action_Add(MenuFlyoutItemBase item)
	{
		ContextMenu.Items.Add(item);
	}

	public void Action_Add(ICommandBarElement item)
	{
		//ContextMenu.SecondaryCommands.Add(item);
	}

	public void Action_Remove(MenuFlyoutItemBase item)
	{
		ContextMenu.Items.Remove(item);
	}

	public void Action_Remove(ICommandBarElement item)
	{
	//	ContextMenu.SecondaryCommands.Remove(item);
	}

	private void EditActionHistory_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		CanUndo = EditActionHistory.Count > 0;
		InvertedEditActionHistory = new(EditActionHistory.Reverse());
	}
	private void TextAction_Copy()
	{
		if (IsSelection)
		{
			DataPackage dataPackage = new DataPackage();
			dataPackage.RequestedOperation = DataPackageOperation.Copy;
			dataPackage.SetText(SelectedText);
			Clipboard.SetContent(dataPackage);
		}
	}

	public void TextAction_Undo(EditAction action = null)
	{
		try
		{
			if (EditActionHistory.Count > 0)
			{
				if (action == null)
				{
					EditAction last = EditActionHistory.Last();
					Text = last.TextState;
					Selection = last.Selection;
					EditActionHistory.Remove(last);
				}
				else
				{
					int index = EditActionHistory.IndexOf(action);
					int end = EditActionHistory.Count - 1;
					for (int i = end; i >= index; i--)
					{
						EditActionHistory.RemoveAt(i);
					}
					Text = action.TextState;
					Selection = action.Selection;
				}
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	public void TextAction_ToggleComment()
	{
		EditActionHistory.Add(new() { EditActionType = EditActionType.Paste, Selection = Selection, TextState = Text, TextInvolved = Language.LineComment });

		Place start = Selection.Start;
		Place end = Selection.End;

		for (int iline = 0; iline < SelectedLines.Count; iline++)
		{
			if (SelectedLines[iline].LineText.StartsWith(Language.LineComment))
			{
				SelectedLines[iline].SetLineText(SelectedLines[iline].LineText.Remove(0, 1));
				if (iline == 0)
					start = start - 1;
				if (iline == SelectedLines.Count - 1)
					end = end - 1;
			}
			else if (SelectedLines[iline].LineText.StartsWith(string.Concat(Enumerable.Repeat("\t", SelectedLines[iline].Indents)) + Language.LineComment))
			{
				SelectedLines[iline].SetLineText(SelectedLines[iline].LineText.Remove(SelectedLines[iline].Indents, 1));
				if (iline == 0)
					start = start - 1;
				if (iline == SelectedLines.Count - 1)
					end = end - 1;
			}
			else
			{
				SelectedLines[iline].SetLineText(SelectedLines[iline].LineText.Insert(SelectedLines[iline].Indents, Language.LineComment));
				if (iline == 0)
					start = start + 1;
				if (iline == SelectedLines.Count - 1)
					end = end + 1;
			}
		}

		Selection = new(start, end);
		CanvasText.Invalidate();
		updateText();
		
		CanvasScrollbarMarkers.Invalidate();
		LinesChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lines)));
	}

	private void TextAction_Delete(Range selection, bool cut = false)
	{
		try
		{
			if (IsSelection)
			{
				EditActionHistory.Add(new() { EditActionType = EditActionType.Delete, Selection = selection, TextState = Text, TextInvolved = SelectedText });

				if (cut)
				{
					TextAction_Copy();
				}
				Place start = selection.VisualStart;
				Place end = selection.VisualEnd;

				string storetext = "";
				int removedlines = 0;
				for (int iLine = start.iLine; iLine <= end.iLine; iLine++)
				{
					if (end.iLine == start.iLine)
					{
						Lines[iLine].SetLineText(Lines[iLine].LineText.Remove(start.iChar, end.iChar - start.iChar));
					}
					else if (iLine == start.iLine)
					{
						if (start.iChar < Lines[iLine].Count)
							Lines[iLine].SetLineText(Lines[iLine].LineText.Remove(start.iChar));
					}
					else if (iLine == end.iLine)
					{
						if (end.iChar == Lines[iLine - removedlines].Count - 1)
							Lines.RemoveAt(iLine - removedlines);
						else
						{
							storetext = Lines[iLine - removedlines].LineText.Substring(end.iChar);
							Lines.RemoveAt(iLine - removedlines);
						}
					}
					else
					{
						Lines.RemoveAt(iLine - removedlines);
						removedlines += 1;
					}
				}
				if (!string.IsNullOrEmpty(storetext))
					Lines[start.iLine].AddToLineText(storetext);
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
		updateText();
		Invalidate();
	}

	private void TextAction_Find()
	{
		IsFindPopupOpen = true;
		if (!SelectedText.Contains("\r\n"))
			Tbx_Search.Text = SelectedText;
		Tbx_Search.Focus(FocusState.Keyboard);
		Tbx_Search.SelectionStart = Tbx_Search.Text.Length;
	}
	private async void TextAction_Paste(string texttopaste = null, Place placetopaste = null, bool updateposition = true, DragDropModifiers dragDropModifiers = DragDropModifiers.None)
	{
		try
		{
			string text = "";
			if (texttopaste == null)
			{
				DataPackageView dataPackageView = Clipboard.GetContent();
				if (dataPackageView.Contains(StandardDataFormats.Text))
				{
					text += await dataPackageView.GetTextAsync();
				}
			}
			else
			{
				text = texttopaste;
			}
			EditActionHistory.Add(new() { TextState = Text, EditActionType = EditActionType.Paste, Selection = Selection, TextInvolved = text?.Replace("\t", @"\t")?.Replace("\n", @"\n") });
			Place place = placetopaste ?? CursorPlace;
			if (IsSelection && place < Selection.VisualStart && dragDropModifiers != DragDropModifiers.Control)
			{
				TextAction_Delete(Selection);
				Selection = new(CursorPlace);
			}
			Language lang = Language;
			int i = 0;
			text = text.Replace("\r\n", "\n");
			int tabcount = Lines[place.iLine].Indents;
			string stringtomove = "";
			string[] pastedlines = text.Split('\n', StringSplitOptions.None);
			foreach (string line in pastedlines)
			{
				if (i == 0 && text.Count(x => x == '\n') == 0)
				{
					if (place.iChar < Lines[place.iLine].LineText.Length)
						Lines[place.iLine].SetLineText(Lines[place.iLine].LineText.Insert(place.iChar, line));
					else
						Lines[place.iLine].SetLineText(Lines[place.iLine].LineText + line);
				}
				else if (i == 0)
				{
					stringtomove = Lines[place.iLine].LineText.Substring(place.iChar);
					Lines[place.iLine].SetLineText(Lines[place.iLine].LineText.Remove(place.iChar) + line);
				}
				else
				{
					var newline = new Line(lang) { LineNumber = place.iLine + 1 + i, IsUnsaved = true };
					newline.SetLineText(string.Concat(Enumerable.Repeat("\t", tabcount)) + line);
					Lines.Insert(place.iLine + i, newline);
				}
				i++;
			}
			if (!string.IsNullOrEmpty(stringtomove))
				Lines[place.iLine + i - 1].AddToLineText(stringtomove);

			if (IsSelection && place >= Selection.VisualEnd && dragDropModifiers != DragDropModifiers.Control)
			{
				TextAction_Delete(Selection);
				if (place.iLine == Selection.VisualEnd.iLine)
					Selection = new(Selection.VisualStart);
			}

			if (updateposition)
			{
				Place end = new(i == 1 ? CursorPlace.iChar + text.Length : pastedlines.Last().Length, place.iLine + i - 1);
				Selection = new(end);
				iCharPosition = CursorPlace.iChar;
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
		updateText();
		Invalidate();
	}

}