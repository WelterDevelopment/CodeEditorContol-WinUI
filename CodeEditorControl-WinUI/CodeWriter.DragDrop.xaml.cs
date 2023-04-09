using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;

namespace CodeEditorControl_WinUI;

public partial class CodeWriter : UserControl, INotifyPropertyChanged
{
	private bool isDragging = false;
	private string draggedText = "";
	private Range draggedSelection;

	private async void TextControl_DragEnter(object sender, DragEventArgs e)
	{
		try
		{
			if (e.DataView.Contains(StandardDataFormats.Text) && e.AllowedOperations != DataPackageOperation.None)
			{
				Place place = await PointToPlace(e.GetPosition(TextControl));
				e.DragUIOverride.IsCaptionVisible = true;
				e.DragUIOverride.IsGlyphVisible = false;
				//	e.AcceptedOperation = DataPackageOperation.Move | DataPackageOperation.Copy;

				string type;
				if (e.Modifiers == DragDropModifiers.Control)
				{
					e.AcceptedOperation = DataPackageOperation.Copy;
					type = "Paste";
				}
				else if (e.DataView.RequestedOperation == DataPackageOperation.Move)
				{
					type = "Move";
					e.AcceptedOperation = DataPackageOperation.Move;
				}
				else
				{
					e.AcceptedOperation = DataPackageOperation.Copy;
					type = "Paste";
				}

				e.DragUIOverride.Caption = $"{type}: {await e.DataView.GetTextAsync()}";

				e.DragUIOverride.IsContentVisible = false;
				IsFocused = true;
				tempFocus = !dragStarted;

				if (IsSelection && (place >= Selection.VisualStart && place < Selection.VisualEnd))
				{
					CursorPlace = Selection.End;
				}
				else
				{
					CursorPlace = place;
				}
			}
			else
			{
				e.AcceptedOperation = DataPackageOperation.None;
			}
			e.Handled = true;
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}
	private async void TextControl_DragOver(object sender, DragEventArgs e)
	{
		try
		{
			if (e.AcceptedOperation != DataPackageOperation.None)
			{
				Place place = await PointToPlace(e.GetPosition(TextControl));
				IsFocused = true;
				if (IsSelection && (place >= Selection.VisualStart && place < Selection.VisualEnd))
				{
					CursorPlace = Selection.End;
				}
				else
				{
					CursorPlace = place;
				}
			}
			e.Handled = true;
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private void TextControl_DragStarting(UIElement sender, DragStartingEventArgs args)
	{
		try
		{
			args.Data.SetText(SelectedText);
			args.Data.RequestedOperation = DataPackageOperation.Move;
			args.AllowedOperations = DataPackageOperation.Move;
			args.DragUI.SetContentFromDataPackage();
			IsFocused = true;
			tempFocus = false;
			dragStarted = true;
			//args.DragUI.SetContentFromSoftwareBitmap(new Windows.Graphics.Imaging.SoftwareBitmap(Windows.Graphics.Imaging.BitmapPixelFormat.Rgba16, 1, 1));
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private async void TextControl_Drop(object sender, DragEventArgs e)
	{
		try
		{
			if (e.DataView.Contains(StandardDataFormats.Text))
			{
				string text = await e.DataView.GetTextAsync();

				TextAction_Paste(text, await PointToPlace(e.GetPosition(TextControl)), true, e.Modifiers);
				e.Handled = true;
				if (tempFocus)
				{
					tempFocus = false;
					dragStarted = false;
					IsFocused = false;

				}
				else
				{
					Focus(FocusState.Pointer);
				}
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

}