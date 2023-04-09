using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using System.IO;
using System.Timers;
using Windows.Foundation;
using Windows.System;

namespace CodeEditorControl_WinUI;

public partial class CodeWriter : UserControl, INotifyPropertyChanged
{
	private Point middleClickScrollingStartPoint = new Point();
	private Point middleClickScrollingEndPoint = new Point();

	public void ScrollToLine(int iLine)
	{
		VerticalScroll.Value = (iLine + 1) * CharHeight - TextControl.ActualHeight / 2;

	}

	public void CenterView()
	{
		HorizontalScroll.Value = 0;
		VerticalScroll.Value = (Selection.VisualStart.iLine + 1) * CharHeight - TextControl.ActualHeight / 2;
		Focus(FocusState.Keyboard);
	}

	private void Scroll_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (isCanvasLoaded)
			Invalidate();
	}

	private void ScrollContent_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
	{
		if (e.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch)
		{
			int scalesign = Math.Sign(e.Delta.Scale - 1);

			FontSize = Math.Min(Math.Max((int)(startFontsize * e.Cumulative.Scale), MinFontSize), MaxFontSize);
			HorizontalScroll.Value -= e.Delta.Translation.X;
			VerticalScroll.Value -= e.Delta.Translation.Y;
		}
	}

	private void ScrollContent_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
	{
		startFontsize = FontSize;
	}

	private void Scroll_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
	{
		try
		{
			PointerPoint pointer = e.GetCurrentPoint(Scroll);
			int mwd = pointer.Properties.MouseWheelDelta;

			if (e.KeyModifiers == VirtualKeyModifiers.Control)
			{
				int newfontsize = FontSize + Math.Sign(mwd);
				if (newfontsize >= MinFontSize && newfontsize <= MaxFontSize)
					SetValue(FontSizeProperty, newfontsize);
			}
			else
			{
				if (pointer.Properties.IsHorizontalMouseWheel)
				{
					if (mwd % 120 == 0) // Mouse
					{
						HorizontalScroll.Value += 6 * mwd / 120 * CharWidth;
					}
					else // Trackpad
					{
						HorizontalScroll.Value += mwd;
					}
				}
				else if (e.KeyModifiers == VirtualKeyModifiers.Shift)
				{
					if (mwd % 120 == 0) // Mouse
					{
						HorizontalScroll.Value -= 3 * mwd / 120 * CharWidth;
					}
					else // Trackpad
					{
						HorizontalScroll.Value -= mwd;
					}
				}
				else
				{
					if (mwd % 120 == 0) // Mouse
					{
						VerticalScroll.Value -= 3 * mwd / 120 * CharHeight;
					}
					else // Trackpad
					{
						VerticalScroll.Value -= mwd;
					}
				}
			}
			IsSuggesting = false;
			e.Handled = true;
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}
	private void VerticalScroll_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		try
		{

			if (e.NewValue == e.OldValue | VisibleLines == null | VisibleLines.Count == 0)
			{
				return;
			}
			int updown = e.NewValue > e.OldValue ? -1 : 0;
			if (Math.Abs((int)e.NewValue - (VisibleLines[0].LineNumber + updown) * CharHeight) < CharHeight)
			{
				return;
			}
			Invalidate();
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}
	private void HorizontalScroll_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		if (e.NewValue == e.OldValue)
		{
			return;
		}
		int n = Math.Max((int)(e.NewValue / CharWidth) * CharWidth, 0);
		iCharOffset = (int)(n / CharWidth);
		HorizontalOffset = -n;
		CanvasBeam.Invalidate();
		CanvasSelection.Invalidate();
		CanvasText.Invalidate();
	}
	private void VerticalScroll_Scroll(object sender, ScrollEventArgs e)
	{
	}

	private void TextControl_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		try
		{
			/* // Proplem: e.GetCurrentPoint() doesn't give the current Point anymore, bug in WinAppSDK 1.1.X; ToDo: Replace zombie code with workaround
			PointerPoint point = e.GetCurrentPoint(TextControl);
			if (point.Properties.IsLeftButtonPressed && isSelecting)
			{
				if (point.Position.Y < 0)
				{
					DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
					Timer.Tick += async (a, b) =>
					{
						PointerPoint pointup = e.GetCurrentPoint(TextControl);
						InfoMessage?.Invoke(this, $"ScrollUp at point: {CurrentPointer.Position} with LeftButtonPressed: {InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftButton)}");

						if (pointup.Position.Y > 0 | InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftButton) == (Windows.UI.Core.CoreVirtualKeyStates.None | Windows.UI.Core.CoreVirtualKeyStates.Locked))
						{
							((DispatcherTimer)a).Stop();
							InfoMessage?.Invoke(this, "ScrollUp Stopped") ;
						}
						VerticalScroll.Value -= CharHeight;
						Selection = new(Selection.Start, await PointToPlace(pointup.Position));
					};
					Timer.Start();
				}
				else if (point.Position.Y > Scroll.ActualHeight - 2 * CharHeight)
				{
					DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
					Timer.Tick += async (a, b) =>
					{
						PointerPoint pointdown = e.GetCurrentPoint(TextControl);
						if (pointdown.Position.Y < Scroll.ActualHeight | !pointdown.Properties.IsLeftButtonPressed)
						{
							((DispatcherTimer)a).Stop();
						}
						VerticalScroll.Value += pointdown.Position.Y - Scroll.ActualHeight;
						Selection = new(Selection.Start, await PointToPlace(pointdown.Position));
					};
					Timer.Start();
				}

				if (point.Position.X < 0)
				{
					DispatcherTimer Timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
					Timer.Tick += async (a, b) =>
					{
						PointerPoint pointleft = e.GetCurrentPoint(TextControl);
						if (pointleft.Position.X > 0 | !pointleft.Properties.IsLeftButtonPressed)
						{
							((Timer)a).Stop();
						}
						HorizontalScroll.Value += pointleft.Position.X;
						Selection = new(Selection.Start, await PointToPlace(pointleft.Position));
					};
					Timer.Start();
				}
				else if (point.Position.X >= Scroll.ActualWidth - 2 * CharWidth)
				{
					DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
					Timer.Tick += async (a, b) =>
					{
						PointerPoint pointright = e.GetCurrentPoint(TextControl);
						if (pointright.Position.X < Scroll.ActualWidth | !pointright.Properties.IsLeftButtonPressed)
						{
							((DispatcherTimer)a).Stop();
						}
						HorizontalScroll.Value += pointright.Position.X - Scroll.ActualWidth;
						Selection = new(Selection.Start, await PointToPlace(pointright.Position));
					};
					Timer.Start();
				}
			}
			e.Handled = true;
			TextControl.Focus(FocusState.Pointer);
			*/
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private void TextControl_PointerLost(object sender, PointerRoutedEventArgs e)
	{
		if (isSelecting)
		{
			e.Handled = true;
			TextControl.Focus(FocusState.Pointer);
		}
		isSelecting = false;
		isLineSelect = false;
		isDragging = false;
		tempFocus = false;
	}
}