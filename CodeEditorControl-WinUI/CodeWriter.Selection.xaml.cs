using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Foundation;

namespace CodeEditorControl_WinUI;

public partial class CodeWriter : UserControl, INotifyPropertyChanged
{
	private Point previousPosition = new Point() { };

	private PointerPoint CurrentPointer { get; set; }
	public void SelectLine(Place start)
	{
		Place end = new(Lines[start.iLine].Count, start.iLine);
		Selection = new(start, end);
	}

	public void TextAction_SelectText(Range range = null)
	{
		if (range == null && Lines.Count > 0)
		{
			Selection = new Range(new(0, 0), new(Lines.Last().Count, Lines.Count - 1));
		}
	}

	private async void TextControl_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		IsSuggesting = false;
		bool hasfocus = Focus(FocusState.Pointer);
		PointerPoint currentpoint = e.GetCurrentPoint(TextControl);
		try
		{

			if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
			{
				Place start = await PointToPlace(currentpoint.Position);
				Place end = new Place(start.iChar, start.iLine);
				var matches = Regex.Matches(Lines[start.iLine].LineText, @"\b\w+?\b");
				foreach (Match match in matches)
				{
					int istart = match.Index;
					int iend = match.Index + match.Length;
					if (start.iChar <= iend && start.iChar >= istart)
					{
						start.iChar = istart;
						end.iChar = iend;
					}
				}
				Selection = new(start, end);
			}
			else if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse | e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
			{
				if (currentpoint.Properties.IsLeftButtonPressed)
				{
					if (IsSelection)
					{
						Place pos = await PointToPlace(currentpoint.Position);
						if (pos > Selection.VisualStart && pos <= Selection.VisualEnd)
						{
							isDragging = true;
							draggedText = SelectedText;
							draggedSelection = new(Selection);
							//e.Handled = true;
							return;
						}
					}
					isLineSelect = currentpoint.Position.X < Width_Left;

					isSelecting = true;
					if (!isLineSelect)
					{
						if (previousPosition != currentpoint.Position)
						{
							Place start = await PointToPlace(currentpoint.Position);
							Place end = await PointToPlace(currentpoint.Position);
							Selection = new(start, end);
							if (CursorPlaceHistory.Count > 0)
							{
								if (end.iLine != CursorPlaceHistory.Last().iLine)
									CursorPlaceHistory.Add(end);
							}
							else
							{
								CursorPlaceHistory.Add(end);
							}
						}
						else
						{
							Place start = await PointToPlace(previousPosition);
							Place end = new Place(start.iChar, start.iLine);
							var matches = Regex.Matches(Lines[start.iLine].LineText, string.Join('|', Language.WordSelectionDefinitions));
							foreach (Match match in matches)
							{
								int istart = match.Index;
								int iend = match.Index + match.Length;
								if (start.iChar <= iend && start.iChar >= istart)
								{
									start.iChar = istart;
									end.iChar = iend;
								}
							}
							Selection = new(start, end);

							

							DoubleClicked?.Invoke(this, new());
						}
						previousPosition = currentpoint.Position;
					}
					else
					{
						Place start = await PointToPlace(currentpoint.Position);
						SelectLine(start);
					}
					isMiddleClickScrolling = false;
					previousPosition = currentpoint.Position;
					iCharPosition = CursorPlace.iChar;
				}
				else if (currentpoint.Properties.IsRightButtonPressed)
				{
					Place rightpress = await PointToPlace(currentpoint.Position);

					Place start = Selection.VisualStart;
					Place end = Selection.VisualEnd;
					if (IsSelection)
					{
						if (rightpress <= start || rightpress >= end)
						{
							Selection = new Range(rightpress);
						}
					}
					else
						Selection = new Range(rightpress);
					isMiddleClickScrolling = false;
				}
				else if (currentpoint.Properties.IsXButton1Pressed)
				{
					if (CursorPlaceHistory.Count > 1)
					{
						Selection = new Range(CursorPlaceHistory[CursorPlaceHistory.Count - 2]);
						CursorPlaceHistory.Remove(CursorPlaceHistory.Last());
					}
				}
				else if (currentpoint.Properties.IsMiddleButtonPressed)
				{
					if (!isMiddleClickScrolling)
					{
						if (VerticalScroll.Maximum + Scroll.ActualHeight > Scroll.ActualHeight && HorizontalScroll.Maximum + Scroll.ActualWidth > Scroll.ActualWidth)
						{
							isMiddleClickScrolling = true;
							ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
							//Cursor = new CoreCursor(CoreCursorType.SizeAll, 1);
							middleClickScrollingStartPoint = currentpoint.Position;
							DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
							Timer.Tick += (a, b) =>
							{
								if (!isMiddleClickScrolling)
								{
									ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
									((DispatcherTimer)a).Stop();
								}
								else
								{
									VerticalScroll.Value += middleClickScrollingEndPoint.Y - middleClickScrollingStartPoint.Y;
									HorizontalScroll.Value += middleClickScrollingEndPoint.X - middleClickScrollingStartPoint.X;
								}
							};
							Timer.Start();
						}
						else if (VerticalScroll.Maximum + Scroll.ActualHeight > Scroll.ActualHeight)
						{
							isMiddleClickScrolling = true;
							ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
							//Cursor = new CoreCursor(CoreCursorType.SizeNorthSouth, 1);
							middleClickScrollingStartPoint = currentpoint.Position;
							DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
							Timer.Tick += (a, b) =>
							{
								if (!isMiddleClickScrolling)
								{
									ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
									((DispatcherTimer)a).Stop();
								}
								else
									VerticalScroll.Value += middleClickScrollingEndPoint.Y - middleClickScrollingStartPoint.Y;
							};
							Timer.Start();
						}
						else if (HorizontalScroll.Maximum + Scroll.ActualWidth > Scroll.ActualWidth)
						{
							isMiddleClickScrolling = true;
							ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
							//Cursor = new CoreCursor(CoreCursorType.SizeWestEast, 1);
							middleClickScrollingStartPoint = currentpoint.Position;
							DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
							Timer.Tick += (a, b) =>
							{
								if (!isMiddleClickScrolling)
								{
									ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
									((DispatcherTimer)a).Stop();
								}
								else
									HorizontalScroll.Value += middleClickScrollingEndPoint.X - middleClickScrollingStartPoint.X;
							};
							Timer.Start();
						}
					}
					else isMiddleClickScrolling = false;
				}
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
		e.Handled = true;
	}

	private async void TextControl_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		try
		{
			CurrentPointerPoint = e.GetCurrentPoint(TextControl);
			if (!isDragging)
				if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse | e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
				{
					Place place = await PointToPlace(CurrentPointerPoint.Position);
					if (isSelecting && CurrentPointerPoint.Properties.IsLeftButtonPressed)
					{
						if (!isLineSelect)
						{
							Selection = new Range(Selection.Start, await PointToPlace(CurrentPointerPoint.Position));
						}
						else
						{
							place.iChar = Lines[place.iLine].Count;
							Selection = new Range(Selection.Start, place);
						}
					}
					else if (isMiddleClickScrolling)
					{
						middleClickScrollingEndPoint = CurrentPointerPoint.Position;
					}
					else
					{
						if (CurrentPointerPoint.Position.X < Width_Left)
						{
							ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
						}
						else if (IsSelection)
						{
							Place start = Selection.VisualStart;
							Place end = Selection.VisualEnd;
							if (place < start || place >= end)
								ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
							else
							{
								if (place.iChar < Lines[place.iLine].Count)
									ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
								else
									ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
							}
						}
						else
							ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
					}
					e.Handled = true;
				}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private async void TextControl_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		try
		{
			PointerPoint currentpoint = e.GetCurrentPoint(TextControl);
			Place place = await PointToPlace(currentpoint.Position);

			isLineSelect = false;

			if (isSelecting)
			{
				isSelecting = false;
				e.Handled = true;
				TextControl.Focus(FocusState.Pointer);
			}

			if (isDragging && place > Selection.VisualStart && place <= Selection.VisualEnd)
			{
				e.Handled = true;
				Selection = new Range(place);
				ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
				Focus(FocusState.Keyboard);
			}
			else if (isDragging)
			{
				Focus(FocusState.Pointer);
			}
			isDragging = false;
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}
}