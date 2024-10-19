using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace CodeEditorControl_WinUI;
public partial class CodeWriter : UserControl, INotifyPropertyChanged
{
	public static CodeWriter Current;
	private new ElementTheme ActualTheme = ElementTheme.Dark;
	private int iCharOffset = 0;
	private int iVisibleChars { get => (int)(((int)TextControl.ActualWidth - Width_Left) / CharWidth); }
	private int iVisibleLines { get => (int)(((int)TextControl.ActualHeight) / CharHeight); }

	private bool IsSettingValue = false;
	private int maxchars = 0;
	public bool IsInitialized = false;


	public CodeWriter()
	{
		InitializeComponent();
		Command_Copy = new RelayCommand(() => TextAction_Copy());
		Command_Paste = new RelayCommand(() => { TextAction_Paste(); });
		Command_Delete = new RelayCommand(() => TextAction_Delete(Selection));
		Command_Cut = new RelayCommand(() => { TextAction_Delete(Selection, true); Selection = new(Selection.VisualStart); });
		Command_SelectAll = new RelayCommand(() => TextAction_SelectText());
		Command_Find = new RelayCommand(() => TextAction_Find());

		Command_Undo = new RelayCommand(() => TextAction_Undo());
		Command_ToggleComment = new RelayCommand(() => TextAction_ToggleComment());

		//EditActionHistory.CollectionChanged += EditActionHistory_CollectionChanged;
		Current = this;
	}

	public event ErrorEventHandler ErrorOccured;

	public event EventHandler<string> InfoMessage;
	public event EventHandler DoubleClicked;

	public event PropertyChangedEventHandler TextChanged;
	public event PropertyChangedEventHandler LinesChanged;
	public event PropertyChangedEventHandler CursorPlaceChanged;

	public event EventHandler Initialized;



	public Place CursorPlace
	{
		get => Get(new Place(0, 0));
		set
		{
			Set(value);
			if (isCanvasLoaded && !isSelecting)
			{
				if (!isLineSelect)
				{
					var width = Scroll.ActualWidth - Width_Left;
					if (value.iChar * CharWidth < HorizontalScroll.Value)
						HorizontalScroll.Value = value.iChar * CharWidth;
					else if ((value.iChar + 3) * CharWidth - width - HorizontalScroll.Value > 0)
						HorizontalScroll.Value = Math.Max((value.iChar + 3) * CharWidth - width, 0);
				}

				if ((value.iLine + 1) * CharHeight <= VerticalScroll.Value)
					VerticalScroll.Value = value.iLine * CharHeight;
				else if ((value.iLine + 2) * CharHeight > VerticalScroll.Value + Scroll.ActualHeight)
					VerticalScroll.Value = Math.Min((value.iLine + 2) * CharHeight - Scroll.ActualHeight, VerticalScroll.Maximum);
			}

			int x = CharWidth * (value.iChar - iCharOffset) + Width_Left;
			int startline = VisibleLines.Count > 0 ? VisibleLines[0].LineNumber : 0;
			int y = CharHeight * (value.iLine - startline + 1);
			CursorPoint = new Point(x, y + CharHeight);

			IsSettingValue = true;
			CurrentLine = value;
			IsSettingValue = false;

			CanvasBeam.Invalidate();
			CanvasScrollbarMarkers.Invalidate();
			//CursorPlaceChanged.Invoke(this,new("CursorPlace"));
		}
	}

	public Point CursorPoint { get => Get(new Point()); set => Set(value); }


	private void OnShowScrollbarsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue)
			HorizontalScroll.Style = VerticalScroll.Style = Resources["AlwaysExpandedScrollBar"] as Style;
		else
		{
			HorizontalScroll.Style = VerticalScroll.Style = Application.Current.Resources["DefaultScrollBarStyle"] as Style;
		}
	}


	public bool IsSelection { get => Get(false); set => Set(value); }

	public bool IsSuggestingOptions { get => Get(false); set => Set(value); }

	public bool IsSuggesting
	{
		get => Get(false);
		set
		{
			Set(value);
			if (value)
			{
				SuggestionIndex = -1;
			}
		}
	}
	public ObservableCollection<Line> Lines { get => Get(new ObservableCollection<Line>()); set => Set(value); }

	public string SelectedText
	{
		get
		{
			string text = "";
			if (Selection.Start == Selection.End)
				return "";

			Place start = Selection.VisualStart;
			Place end = Selection.VisualEnd;

			if (start.iLine == end.iLine)
			{
				text = Lines[start.iLine].LineText.Substring(start.iChar, end.iChar - start.iChar);
			}
			else
			{
				for (int iLine = start.iLine; iLine <= end.iLine; iLine++)
				{
					if (iLine == start.iLine)
						text += Lines[iLine].LineText.Substring(start.iChar) + "\r\n";
					else if (iLine == end.iLine)
						text += Lines[iLine].LineText.Substring(0, end.iChar);
					else
						text += Lines[iLine].LineText + "\r\n";
				}
			}

			return text;
		}
	}

	public List<Line> SelectedLines = new();

	public Range Selection
	{
		get => Get(new Range(CursorPlace, CursorPlace));
		set
		{
			Set(value);
			CursorPlace = new Place(value.End.iChar, value.End.iLine);
			IsSelection = value.Start != value.End;
			SelectedLines = Lines.ToList().Where(x => x != null && x.iLine >= value.VisualStart.iLine && x.iLine <= value.VisualEnd.iLine).ToList();
			CanvasSelection.Invalidate();
		}
	}



	public List<SyntaxError> SyntaxErrors
	{
		get => Get(new List<SyntaxError>()); set
		{
			Set(value);
			DispatcherQueue.TryEnqueue(() =>
			{
				CanvasScrollbarMarkers.Invalidate(); CanvasText.Invalidate();
			});
		}
	}



	public List<Line> VisibleLines { get; set; } = new List<Line>();





	private bool isCanvasLoaded { get => CanvasText.IsLoaded; }

	private bool isLineSelect { get; set; } = false;

	private bool isMiddleClickScrolling { get => Get(false); set { Set(value); } }
	private bool IsFocused { get => Get(false); set { Set(value); } }

	private bool isSelecting { get; set; } = false;









	public Char this[Place place]
	{
		get => Lines[place.iLine][place.iChar];
		set => Lines[place.iLine][place.iChar] = value;
	}

	public Line this[int iLine]
	{
		get { return Lines[iLine]; }
	}

	public static int IntLength(int i)
	{
		if (i < 0)
			return 1;
		if (i == 0)
			return 1;
		return (int)Math.Floor(Math.Log10(i)) + 1;
	}




	public void RedrawText()
	{
		try
		{
			CanvasText.Invalidate();
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	public void Invalidate(bool sizechanged = false)
	{
		try
		{
			DrawText(sizechanged);
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	public async void Save()
	{
		await Task.Run(() =>
		{
			foreach (Line line in new List<Line>(Lines))
			{
				line.Save();
			}
			SyntaxErrors.Clear();
		});

		IsSuggesting = false;
		IsSuggestingOptions = false;

		CanvasText.Invalidate();
		CanvasScrollbarMarkers.Invalidate();
	}







	private void CanvasBeam_Draw(CanvasControl sender, CanvasDrawEventArgs args)
	{
		try
		{
			if (VisibleLines.Count > 0)
			{
				int x = (int)(Width_Left + HorizontalOffset + CursorPlace.iChar * CharWidth);
				int y = (int)((CursorPlace.iLine - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);

				for (int i = 0; i < CursorPlace.iChar; i++)
				{
					if (Lines.Count > CursorPlace.iLine)
						if (Lines[CursorPlace.iLine].Count > i)
							if (Lines[CursorPlace.iLine][i].C == '\t')
							{
								x += CharWidth * (TabLength - 1);
							}
				}

				Point point = PlaceToPoint(CursorPlace);
				y = (int)point.Y;
				x = (int)point.X;

				if (Selection.Start == CursorPlace)
				{
					//args.DrawingSession.DrawRoundedRectangle(Width_Left, y, (int)TextControl.ActualWidth - Width_Left, CharHeight, 2, 2, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2f);
					args.DrawingSession.FillRectangle(Width_Left, y, (int)TextControl.ActualWidth - Width_Left, CharHeight, ActualTheme == ElementTheme.Light ? Color_SelelectedLineBackground.InvertColorBrightness() : Color_SelelectedLineBackground);
				}

				if (y <= TextControl.ActualHeight && y >= 0 && x <= TextControl.ActualWidth && x >= Width_Left)
					args.DrawingSession.DrawLine(new Vector2(x, y), new Vector2(x, y + CharHeight), ActualTheme == ElementTheme.Light ? Color_Beam.InvertColorBrightness() : Color_Beam, 2f);


				int xms = (int)(Width_Left);
				int iCharStart = iCharOffset;
				int xme = (int)TextControl.ActualWidth;
				int iCharEnd = iCharStart + (int)((xme - xms) / CharWidth);

				if (ShowHorizontalTicks)
					for (int iChar = iCharStart; iChar < iCharEnd; iChar++)
					{
						int xs = (int)((iChar - iCharStart) * CharWidth) + xms;
						if (iChar % 10 == 0)
							args.DrawingSession.DrawLine(xs, 0, xs, CharHeight / 8, new CanvasSolidColorBrush(sender, Color_LineNumber), 2f);
					}
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private void CanvasScrollbarMarkers_Draw(CanvasControl sender, CanvasDrawEventArgs args)
	{
		try
		{
			if (ShowScrollbarMarkers)
			{
				float markersize = (float)Math.Max(CharHeight / (VerticalScroll.Maximum + ScrollContent.ActualHeight) * CanvasScrollbarMarkers.ActualHeight, 4f);
				float width = (float)VerticalScroll.ActualWidth;
				float height = (float)CanvasScrollbarMarkers.ActualHeight;
				float linecount = Lines.Count;

				foreach (SearchMatch search in SearchMatches?.ToArray())
					args.DrawingSession.DrawLine(width / 3f, search.iLine / linecount * height, width * 2 / 3f, search.iLine / linecount * height, ActualTheme == ElementTheme.Light ? Colors.LightGray.ChangeColorBrightness(-0.3f) : Colors.LightGray, markersize);

				foreach (Line line in Lines?.ToArray()?.Where(x => x.IsUnsaved))
					args.DrawingSession.DrawLine(0, line.iLine / linecount * height, width * 1 / 3f, line.iLine / linecount * height, ActualTheme == ElementTheme.Light ? Color_UnsavedMarker.ChangeColorBrightness(-0.2f) : Color_UnsavedMarker, markersize);

				foreach (SyntaxError error in SyntaxErrors?.ToArray())
				{
					if (error.SyntaxErrorType == SyntaxErrorType.Error)
					{
						args.DrawingSession.DrawLine(width * 2 / 3f, error.iLine / linecount * height, width, error.iLine / linecount * height, ActualTheme == ElementTheme.Light ? Colors.Red.ChangeColorBrightness(-0.2f) : Colors.Red, markersize);
					}
					else if (error.SyntaxErrorType == SyntaxErrorType.Warning)
					{
						args.DrawingSession.DrawLine(width * 2 / 3f, error.iLine / linecount * height, width, error.iLine / linecount * height, ActualTheme == ElementTheme.Light ? Colors.Yellow.ChangeColorBrightness(-0.2f) : Colors.Yellow, markersize);
					}
				}

				float cursorY = CursorPlace.iLine / linecount * height;
				args.DrawingSession.DrawLine(0, cursorY, width, cursorY, ActualTheme == ElementTheme.Light ? Color_Beam.InvertColorBrightness() : Color_Beam, 2f);
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private void CanvasSelection_Draw(CanvasControl sender, CanvasDrawEventArgs args)
	{
		try
		{
			if (VisibleLines.Count > 0)
			{
				if (IsSelection)
				{
					Place start = Selection.VisualStart;
					Place end = Selection.VisualEnd;

					if (start.iLine < VisibleLines[0].iLine)
					{
						start.iLine = VisibleLines[0].iLine;
						start.iChar = 0;
					}

					if (end.iLine > VisibleLines.Last().iLine)
					{
						end.iLine = VisibleLines.Last().iLine;
						end.iChar = VisibleLines.Last().Count;
					}

					for (int lp = start.iLine; lp <= end.iLine; lp++)
						if (lp >= VisibleLines[0].iLine && lp <= VisibleLines.Last().iLine)
							if (start.iLine == end.iLine)
								DrawSelection(args.DrawingSession, start.iLine, start.iChar, end.iChar);
							else if (lp == start.iLine)
								DrawSelection(args.DrawingSession, lp, start.iChar, Lines[lp].Count + 1);
							else if (lp > start.iLine && lp < end.iLine)
								DrawSelection(args.DrawingSession, lp, 0, Lines[lp].Count + 1);
							else if (lp == end.iLine)
								DrawSelection(args.DrawingSession, lp, 0, end.iChar);
				}

				foreach (SearchMatch match in new List<SearchMatch>(SearchMatches))
				{
					if (match.iLine >= VisibleLines[0].iLine && match.iLine <= VisibleLines.Last().iLine)
						DrawSelection(args.DrawingSession, match.iLine, match.iChar, match.iChar + match.Match.Length, SelectionType.SearchMatch);
				}
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private void CanvasLineInfo_Draw(CanvasControl sender, CanvasDrawEventArgs args)
	{
		try
		{
			sender.DpiScale = XamlRoot.RasterizationScale > 1.0d ? 1.15f : 1.0f; // The text was shaking around on text input at Scale factors > 1. Setting DpiScale seems to prevent this.
			args.DrawingSession.Antialiasing = CanvasAntialiasing.Aliased;
			//args.DrawingSession.Blend = CanvasBlend.Add;
			//args.DrawingSession.TextAntialiasing = CanvasTextAntialiasing.ClearType;
			if (VisibleLines.Count > 0)
			{
				int foldPos = Width_LeftMargin + Width_LineNumber + Width_ErrorMarker + Width_WarningMarker;
				int errorPos = Width_LeftMargin + Width_LineNumber;
				int warningPos = errorPos + Width_ErrorMarker;
				int totalwraps = 0;
				float thickness = Math.Max(1, CharWidth / 6f);
				var folds = foldings?.ToList();
				var lines = Lines?.ToList();
				var vislines = VisibleLines?.ToList();

				if (lines == null | lines.Count == 0 | vislines == null | vislines.Count == 0)
					return;

				for (int iLine = vislines[0].iLine; iLine < vislines.Last().LineNumber; iLine++)
				{
					int y = CharHeight * (iLine - vislines[0].LineNumber + 1 + totalwraps);
					int x = 0;
					args.DrawingSession.FillRectangle(0, y, Width_Left - Width_TextIndent, CharHeight, Color_LeftBackground);

					if (ShowLineNumbers)
						args.DrawingSession.DrawText((iLine + 1).ToString(), CharWidth * IntLength(lines.Count) + Width_LeftMargin, y, ActualTheme == ElementTheme.Light ? Color_LineNumber.InvertColorBrightness() : Color_LineNumber, new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize, HorizontalAlignment = CanvasHorizontalAlignment.Right });
					if (IsFoldingEnabled && Language.FoldingPairs != null)
					{

						if (folds.Any(x => x.StartLine == iLine))
						{
							//args.DrawingSession.FillCircle(foldPos + CharWidth / 2, y + CharHeight / 2, CharWidth / 3, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker);
							float w = CharWidth * 0.75f;
							args.DrawingSession.FillRectangle(foldPos + (CharWidth - w) / 2f, y + CharHeight / 2 - w / 2f, w, w, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker);
							//args.DrawingSession.DrawLine(foldPos + CharWidth / 4, y + CharHeight / 2, foldPos + CharWidth * 3 / 4, y + CharHeight / 2, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
						}

						//else if ()
						//{
						//	args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y, foldPos + CharWidth / 2, y + CharHeight, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
						//	args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y + CharHeight / 2, foldPos + CharWidth, y + CharHeight / 2, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
						//}
						else if (folds.Any(x => x.Endline == iLine))
						{
							args.DrawingSession.DrawLine(foldPos + CharWidth / 2f - thickness / 2f, y + CharHeight / 2f, foldPos + CharWidth, y + CharHeight / 2f, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, thickness);
							args.DrawingSession.DrawLine(foldPos + CharWidth / 2f, y, foldPos + CharWidth / 2f, y + CharHeight / 2f, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, thickness);
						}

						if (folds.Any(x => iLine > x.StartLine && iLine < x.Endline))
						{
							args.DrawingSession.DrawLine(foldPos + CharWidth / 2f, y - CharHeight / 2f, foldPos + CharWidth / 2f, y + CharHeight * 1.5f, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, thickness);
						}
					}

					if (ShowLineMarkers)
					{
						if (lines[iLine].IsUnsaved)
							args.DrawingSession.FillRectangle(warningPos, y, Width_ErrorMarker, CharHeight, ActualTheme == ElementTheme.Light ? Color_UnsavedMarker.ChangeColorBrightness(-0.2f) : Color_UnsavedMarker);

						if (SyntaxErrors.Any(x => x.iLine == iLine))
						{
							SyntaxError lineError = SyntaxErrors.First(x => x.iLine == iLine);
							if (lineError.SyntaxErrorType == SyntaxErrorType.Error)
							{
								args.DrawingSession.FillRectangle(errorPos, y, Width_ErrorMarker, CharHeight, Color.FromArgb(255, 200, 40, 40));
							}
							if (lineError.SyntaxErrorType == SyntaxErrorType.Warning)
							{
								args.DrawingSession.FillRectangle(warningPos, y, Width_WarningMarker, CharHeight, Color.FromArgb(255, 180, 180, 40));
							}
						}
					}


				}
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private void CanvasText_Draw(CanvasControl sender, CanvasDrawEventArgs args)
	{
		try
		{
			sender.DpiScale = XamlRoot.RasterizationScale > 1.0d ? 1.15f : 1.0f; // The text was shaking around on text input at Scale factors > 1. Setting DpiScale seems to prevent this.
			args.DrawingSession.Antialiasing = CanvasAntialiasing.Aliased;
			//args.DrawingSession.Blend = CanvasBlend.Add;
			//args.DrawingSession.TextAntialiasing = CanvasTextAntialiasing.ClearType;
			if (VisibleLines.Count > 0)
			{
				int foldPos = Width_LeftMargin + Width_LineNumber + Width_ErrorMarker + Width_WarningMarker;
				int errorPos = Width_LeftMargin + Width_LineNumber;
				int warningPos = errorPos + Width_ErrorMarker;
				int totalwraps = 0;
				float thickness = Math.Max(1, CharWidth / 6f);
				var folds = foldings.ToList();
				var lines = Lines.ToList();

				for (int iLine = VisibleLines[0].iLine; iLine < VisibleLines.Last().LineNumber; iLine++)
				{
					int y = CharHeight * (iLine - VisibleLines[0].LineNumber + 1 + totalwraps);
					int x = 0;
					int lastChar = IsWrappingEnabled ? lines[iLine].Count : Math.Min(iCharOffset + ((int)Scroll.ActualWidth - Width_Left) / CharWidth, lines[iLine].Count);
					int indents = 0;

					int textWrappingLines = lines[iLine].Count / ((int)Scroll.ActualWidth - Width_Left);
					int linewraps = 0;
					int wrapindent = 0;
					int iWrappingChar = 0;

					if (IsWrappingEnabled)
					{
						for (int iWrappedLine = 0; iWrappedLine < lines[iLine].WrappedLines.Count; iWrappedLine++)
						{
							var wrappedLine = lines[iLine].WrappedLines[iWrappedLine];
							for (int iChar = 0; iChar < wrappedLine.Count; iChar++)
							{
								Char c = wrappedLine[iChar];

								if (c.C == '\t')
								{
									x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
									indents += 1;
								}
								else if (iChar >= iCharOffset - indents * (TabLength - 1))
								{

									//	int iWrappingEndPosition = (linewraps + 1) * maxchar - (WrappingLength * linewraps) - (indents * (TabLength - 1) * (linewraps + 1));

									if (iChar > 0 && iChar < wrappedLine.Count)
										iWrappingChar++;


									x = Width_Left + CharWidth * (iWrappingChar - iCharOffset + indents * (TabLength - 1));
									//}

									if (c.T == Token.Key)
									{
										if (IsInsideBrackets(new(iChar, iLine)))
										{
											args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[Token.Key].InvertColorBrightness() : EditorOptions.TokenColors[Token.Key], new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize });
										}
										else
										{
											args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[Token.Normal].InvertColorBrightness() : EditorOptions.TokenColors[Token.Normal], new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize });
										}
									}
									else
									{
										args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[c.T].InvertColorBrightness() : EditorOptions.TokenColors[c.T], new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize });
									}

								}

							}
							if (iWrappedLine < lines[iLine].WrappedLines.Count - 1)
							{
								y += CharHeight;
								iWrappingChar = WrappingLength;
								totalwraps++;
								linewraps++;
								args.DrawingSession.FillRectangle(0, y, Width_Left - Width_TextIndent, CharHeight, Color_LeftBackground);
							}
						}
					}
					else
						for (int iChar = 0; iChar < lastChar; iChar++)
						{
							Char c = lines[iLine][iChar];

							if (c.C == '\t')
							{
								x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
								indents += 1;
								if (ShowControlCharacters)
									if (iChar >= iCharOffset - indents * (TabLength - 1)) // Draw indent arrows
									{
										CanvasPathBuilder pathBuilder = new CanvasPathBuilder(sender);

										pathBuilder.BeginFigure(CharWidth * 0.2f, CharHeight / 2);
										pathBuilder.AddLine(CharWidth * (TabLength - 0.2f), CharHeight / 2);
										pathBuilder.EndFigure(CanvasFigureLoop.Open);

										pathBuilder.BeginFigure(CharWidth * (TabLength - 0.5f), CharHeight * 1 / 4);
										pathBuilder.AddLine(CharWidth * (TabLength - 0.2f), CharHeight / 2);
										pathBuilder.AddLine(CharWidth * (TabLength - 0.5f), CharHeight * 3 / 4);
										pathBuilder.EndFigure(CanvasFigureLoop.Open);

										CanvasGeometry arrow = CanvasGeometry.CreatePath(pathBuilder);

										args.DrawingSession.DrawGeometry(arrow, x, y, ActualTheme == ElementTheme.Light ? Color_WeakMarker.InvertColorBrightness() : Color_WeakMarker, thickness);
									}
								if (ShowIndentGuides != IndentGuide.None)
								{
									if (iChar >= iCharOffset - indents * (TabLength - 1)) // Draw indent arrows
									{
										args.DrawingSession.DrawLine(x + CharWidth / 3f, y, x + CharWidth / 3f, y + CharHeight, ActualTheme == ElementTheme.Light ? Color_FoldingMarkerUnselected.InvertColorBrightness() : Color_FoldingMarkerUnselected, 1.5f, new CanvasStrokeStyle() { DashStyle = ShowIndentGuides == IndentGuide.Line ? CanvasDashStyle.Solid : CanvasDashStyle.Dash });
									}
								}
							}
							else if (iChar >= iCharOffset - indents * (TabLength - 1))
							{
								if (!IsWrappingEnabled && iChar < iCharOffset - indents * (TabLength - 1) + iVisibleChars)
								{
									x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
									if (c.T == Token.Key)
									{
										if (IsInsideBrackets(new(iChar, iLine)))
										{
											args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[Token.Key].InvertColorBrightness() : EditorOptions.TokenColors[Token.Key], new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize });
										}
										else
										{
											args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[Token.Normal].InvertColorBrightness() : EditorOptions.TokenColors[Token.Normal], new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize });
										}
									}
									else
									{
										args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[c.T].InvertColorBrightness() : EditorOptions.TokenColors[c.T], new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize });
									}
								}

								if (IsWrappingEnabled)
								{
									int maxchar = iVisibleChars;
									//if (iChar + indents * (TabLength - 1) - iCharOffset < maxchar)
									//{
									//	x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
									//}
									//else
									//{
									int iWrappingEndPosition = (linewraps + 1) * maxchar - (3 * linewraps) - (indents * (TabLength - 1) * (linewraps + 1));

									if (iChar > 0 && iChar < iWrappingEndPosition)
										iWrappingChar++;

									if (iChar == iWrappingEndPosition)
									{
										y += CharHeight;
										iWrappingChar = 3;
										totalwraps++;
										linewraps++;
										args.DrawingSession.FillRectangle(0, y, Width_Left - Width_TextIndent, CharHeight, Color_LeftBackground);
										//wrapindent = 1;
									}

									//if (iChar > 0)
									//	if ((iChar + indents * (TabLength - 1) - iCharOffset) % (maxchar) == 0)
									//	{
									//		y += CharHeight;
									//		totalwraps++;
									//		linewraps++;
									//		wrapindent = 1;
									//	}
									x = Width_Left + CharWidth * (iWrappingChar - iCharOffset + indents * (TabLength - 1));
									//}

									args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[c.T].InvertColorBrightness() : EditorOptions.TokenColors[c.T], new CanvasTextFormat() { FontFamily = FontUri, FontSize = ScaledFontSize });
								}

							}
						}
					if (ShowControlCharacters && iLine < lines.Count - 1 && lastChar >= iCharOffset - indents * (TabLength - 1))
					{
						x = Width_Left + CharWidth * (lastChar + indents * (TabLength - 1) - iCharOffset);
						CanvasPathBuilder enterpath = new CanvasPathBuilder(sender);

						enterpath.BeginFigure(CharWidth * 0.9f, CharHeight * 1 / 3);
						enterpath.AddLine(CharWidth * 0.9f, CharHeight * 3 / 4);
						enterpath.AddLine(CharWidth * 0.0f, CharHeight * 3 / 4);
						enterpath.EndFigure(CanvasFigureLoop.Open);

						enterpath.BeginFigure(CharWidth * 0.4f, CharHeight * 2 / 4);
						enterpath.AddLine(CharWidth * 0.1f, CharHeight * 3 / 4);
						enterpath.AddLine(CharWidth * 0.4f, CharHeight * 4 / 4);
						enterpath.EndFigure(CanvasFigureLoop.Open);

						CanvasGeometry enter = CanvasGeometry.CreatePath(enterpath);

						args.DrawingSession.DrawGeometry(enter, x, y, ActualTheme == ElementTheme.Light ? Color_WeakMarker.InvertColorBrightness() : Color_WeakMarker, thickness);
					}
				}
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	private async void CodeWriter_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
	{
		try
		{
			if (isSelecting) return;
			if (char.IsLetterOrDigit(args.Character) | char.IsSymbol(args.Character) | char.IsPunctuation(args.Character) | char.IsSeparator(args.Character) | char.IsSurrogate(args.Character))
			{
				if (IsSelection)
				{
					TextAction_Delete(Selection);
					Selection = new(Selection.VisualStart);
				}

				if (Language.CommandTriggerCharacters.Contains(args.Character))
				{
					SuggestionStart = CursorPlace;
					//Suggestions = Commands;
					SuggestionIndex = -1;

					IsSuggesting = true;
					IsSuggestingOptions = false;
					Lbx_Suggestions.ScrollIntoView(Suggestions?.FirstOrDefault());
				}
				else if (!char.IsLetter(args.Character))
				{
					IsSuggesting = false;
					IsSuggestingOptions = false;
				}

				if (args.Character == ' ')
				{
					EditActionHistory.Add(new() { EditActionType = EditActionType.Add, TextInvolved = " ", Selection = Selection, TextState = Text });
					if (!IsSuggestingOptions)
					{
						IsSuggesting = false;
						IsSuggestingOptions = false;
					}
				}
				else
				{
					if (EditActionHistory.Count > 0)
					{
						EditAction last = EditActionHistory.Last();
						if (last.EditActionType == EditActionType.Add)
						{
							last.TextInvolved += args.Character.ToString();
						}
						else
						{
							EditActionHistory.Add(new() { TextInvolved = args.Character.ToString(), TextState = Text, EditActionType = EditActionType.Add, Selection = Selection });
						}
					}
					else
					{
						EditActionHistory.Add(new() { TextInvolved = args.Character.ToString(), TextState = Text, EditActionType = EditActionType.Add, Selection = Selection });
					}
				}

				Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, args.Character.ToString()));


				if (Language.EnableIntelliSense)
					if (((args.Character == ',' | args.Character == ' ') && IsInsideBrackets(CursorPlace)) | Language.OptionsTriggerCharacters.Contains(args.Character))
					{
						IsSuggestingOptions = true;
						SuggestionStart = CursorPlace;
						var command = GetCommandAtPosition(CursorPlace);
						IntelliSense intelliSense = command.Command;
						var argument = command.ArgumentsRanges?.FirstOrDefault(x => CursorPlace.iChar >= x.Start.iChar && CursorPlace.iChar <= x.End.iChar);
						if (argument != null)
						{
							int argumentindex = command.ArgumentsRanges.IndexOf(argument);
							//IntelliSense intelliSense = GetCommandFromPlace(CursorPlace);
							if (intelliSense != null && argumentindex != -1)
							{
								if (intelliSense.ArgumentsList?.Count > argumentindex)
								{
									SuggestionStart = CursorPlace + 1;
									Options = intelliSense.ArgumentsList[argumentindex]?.Parameters;
									AllOptions = Suggestions = Options.Select(x =>
									{
										if (x is KeyValue keyValue)
										{
											keyValue.Snippet = "=";
											string options = "";
											if (keyValue.Values != null)
											{
												if (keyValue.Values.Count > 5)
													options = string.Join("|", keyValue.Values.Take(5)) + "|...";
												else
													options = string.Join("|", keyValue.Values);
												keyValue.Options = options;
											}
											keyValue.IntelliSenseType = IntelliSenseType.Argument;
										}
										return (Suggestion)x;
									}
									).ToList();
									IsSuggesting = true;
								}
							}
						}
					}

				if (Language.AutoClosingPairs.Keys.Contains(args.Character))
				{
					if (CursorPlace.iChar == Lines[CursorPlace.iLine].Count)
						Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText + Language.AutoClosingPairs[args.Character]);
					else //if (Lines[CursorPlace.iLine].LineText[CursorPlace.iChar + 1] == ' ')
						Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar + 1, Language.AutoClosingPairs[args.Character].ToString()));
				}


				Selection = new(Selection.VisualStart + 1);
				iCharPosition = CursorPlace.iChar;


				CanvasText.Invalidate();
				updateText();
				//textChanged();
				FilterSuggestions();

				IsFindPopupOpen = false;
				args.Handled = true;
			}
		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}

	}





	private int GetWrappingLinesOffset(int iline)
	{
		int visualline = 0;
		int ilineoffset = 0;
		int wraplines = 0;
		if (IsWrappingEnabled)
		{
			Action getLine = delegate //() =>
			{
				int iwrappedLines = 0;
				foreach (var line in VisibleLines)
				{
					for (int wrappingLine = 0; wrappingLine < line.WrappedLines.Count; wrappingLine++)
					{

						if (wrappingLine != 0)
							iwrappedLines++;
						if (visualline == iline + iwrappedLines)
						{
							wraplines = iwrappedLines;
							return;
						}
						visualline++;
					}
				}
			};
			getLine();
			return wraplines;
		}
		else return 0;
	}

	private void DrawSelection(CanvasDrawingSession session, int Line, int StartChar, int EndChar, SelectionType selectionType = SelectionType.Selection)
	{
		int xtaboffset = 0;
		for (int i = 0; i < EndChar; i++)
		{
			if (i < Lines[Line].Count - 1 && Lines[Line][i].C == '\t' && i < StartChar)
			{
				xtaboffset += CharWidth * (TabLength - 1);
			}
			if (i >= StartChar)
			{
				int x = (int)(Width_Left + HorizontalOffset + i * CharWidth) + xtaboffset;
				int y = (int)((Line - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
				y += GetWrappingLinesOffset(Line);
				if (i < Lines[Line].Count - 1 && Lines[Line][i].C == '\t')
				{
					xtaboffset += CharWidth * (TabLength - 1);
					DrawSelectionCell(session, x, y, selectionType, TabLength);
				}
				else
					DrawSelectionCell(session, x, y, selectionType);
			}
		}
	}

	private void DrawSelectionCell(CanvasDrawingSession session, int x, int y, SelectionType selectionType, int w = 1)
	{
		Color color = Color_Selection;
		if (selectionType == SelectionType.SearchMatch)
		{
			color = Color.FromArgb(255, 60, 60, 60);
		}
		else if (selectionType == SelectionType.WordLight)
		{
			color = Color.FromArgb(255, 60, 60, 200);
		}
		session.FillRectangle(x, y, CharWidth * w + 1, CharHeight + 1, ActualTheme == ElementTheme.Light ? color.InvertColorBrightness() : color);
	}

	private async void DrawText(bool sizechanged = false)
	{
		if (sizechanged)
		{
			TextFormat = new CanvasTextFormat()
			{
				FontFamily = FontUri,
				FontSize = ScaledFontSize
			};
			Size size = MeasureTextSize(CanvasDevice.GetSharedDevice(), "M", TextFormat);
			Size sizew = MeasureTextSize(CanvasDevice.GetSharedDevice(), "M", TextFormat);
			CharHeight = (int)(size.Height * 2f);
			float widthfactor = 1.1f;
			if (Font.StartsWith("Cascadia"))
			{
				widthfactor = 1.35f;
			}
			CharWidth = (int)(sizew.Width * widthfactor);
		}

		if (VerticalScroll != null && HorizontalScroll != null && Lines != null)
		{
			int StartLine = (int)(VerticalScroll.Value / CharHeight) + 1;
			int EndLine = Math.Min((int)((VerticalScroll.Value + Scroll.ActualHeight) / CharHeight) + 1, Lines.Count);

			//LinesControl.ScrollToVerticalOffset(VerticalScroll.Value);

			maxchars = 0;

			for (int i = 0; i < Lines.Count; i++)
			{
				Line l = Lines[i];
				maxchars = Math.Max(l.Count + l.Indents * (TabLength - 1) + 1, maxchars);
			}

			VisibleLines.Clear();

			for (int i = 0; i < Lines.Count; i++)
			{
				Line l = Lines[i];
				if (l != null && l.LineNumber <= EndLine && l.LineNumber >= StartLine)
				{
					VisibleLines.Add(l);
				}
			}

			Width_LeftMargin = ShowLineNumbers ? CharWidth : 0;
			Width_LineNumber = ShowLineNumbers ? CharWidth * IntLength(Lines.Count) : 0;
			Width_FoldingMarker = IsFoldingEnabled && Language.FoldingPairs != null ? CharWidth : 0;
			Width_ErrorMarker = ShowLineNumbers ? CharWidth / 2 : 0;
			Width_WarningMarker = ShowLineMarkers ? CharWidth / 2 : 0;

			VerticalScroll.Maximum = (Lines.Count + 2) * CharHeight - Scroll.ActualHeight;
			VerticalScroll.SmallChange = CharHeight;
			VerticalScroll.LargeChange = CharHeight;
			VerticalScroll.Visibility = Lines.Count * CharHeight > TextControl.ActualHeight ? Visibility.Visible : Visibility.Collapsed;

			HorizontalScroll.SmallChange = CharWidth;
			HorizontalScroll.LargeChange = CharWidth;

			if (!IsWrappingEnabled)
			{
				HorizontalScroll.Maximum = (maxchars + 1) * CharWidth - Scroll.ActualWidth + Width_Left;
				HorizontalScroll.Visibility = maxchars * CharWidth > TextControl.ActualWidth ? Visibility.Visible : Visibility.Collapsed;
			}
			else
			{
				HorizontalScroll.Maximum = 0;
				HorizontalScroll.Visibility = Visibility.Collapsed;
			}

			VerticalScroll.ViewportSize = TextControl.ActualHeight;
			HorizontalScroll.ViewportSize = TextControl.ActualWidth;


			DispatcherQueue.TryEnqueue(() =>
			{
				CalculateLineWraps();
				CanvasBeam.Invalidate();
				CanvasSelection.Invalidate();
				CanvasText.Invalidate();
				CanvasScrollbarMarkers.Invalidate();
				CanvasLineInfo.Invalidate();
			});
		}
	}

	private void CalculateLineWraps()
	{
		try
		{
			lock (Lines)
			{
				var lines = new List<Line>(Lines);
				foreach (Line line in lines)
				{
					line.CalculateWrappedLines(iVisibleChars, TabLength, WrappingLength);
				}
			}
		}
		catch { }
	}


	// ToDo: Hier gibts einen Fehler, wenn kompiliert mit x64 - Release
	private CanvasTextFormat TextFormat { get; set; }

	private DispatcherTimer TextChangedTimer { get; set; } = new() { Interval = TimeSpan.FromMilliseconds(100) };
	private string TextChangedTimerLastText { get; set; } = "";

	private async Task InitializeLines(string text)
	{
		VisibleLines.Clear();
		Lines.Clear();
		CursorPlaceHistory.Clear();
		IsFindPopupOpen = false;

		Language lang = Language;
		string name = lang.Name;

		int lastVisibleLine = (int)CanvasText.ActualHeight / CharHeight;

		if (text == null) return;

		await Task.Run(async () =>
		{
			text = text.Replace("\r\n", "\n").Replace("\r", "\n");
			string[] lines = text.Split("\n", StringSplitOptions.None);

			lastVisibleLine = Math.Min(lines.Length, lastVisibleLine + 5);
			int lineNumber = 1;
			bool innerLang = false;


			foreach (string line in lines)
			{
				Language lg = lang;

				if (lang.NestedLanguages != null)
					foreach (var nestedlang in lang.NestedLanguages)
					{
						Match endmatch = Regex.Match(line, nestedlang.RegexEnd);
						if (endmatch.Success)
						{
							innerLang = false;
						}
						if (innerLang)
						{
							lg = Languages.LanguageList.FirstOrDefault(x => x.Name == nestedlang.InnerLanguage) ?? lang;
						}
						else
						{
							lg = lang;
						}
						Match startmatch = Regex.Match(line, nestedlang.RegexStart);
						if (startmatch.Success)
						{
							innerLang = true;
						}
					}
				Line l = new Line(lg) { LineNumber = lineNumber };
				l.SetLineText(line);
				l.Save();
				Lines.Add(l);
				lineNumber++;
				if (lineNumber == lastVisibleLine)
				{
					DispatcherQueue.TryEnqueue(() =>
								{
									Invalidate(true);
								});
				}
			}

			DispatcherQueue.TryEnqueue(() =>
						{
							if (isCanvasLoaded && Lines != null && Lines.Count > 0)
							{
								updateFoldingPairs(Language);
								Invalidate(true);
								if (!IsInitialized)
								{
									IsInitialized = true;
									Initialized?.Invoke(this, null);
								}
							}
						});
		});



	}

	private bool invoked = false;
	private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
	{
		if (invoked)
		{
			args.Handled = true;
			invoked = false;
		}
		else
		{
			invoked = true;
		}
	}

	private void EditActionHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var history = (e.NewValue as ObservableCollection<EditAction>);
		history.CollectionChanged += EditActionHistory_CollectionChanged;
		CanUndo = history.Count > 0;
		InvertedEditActionHistory = new(history.Reverse());
	}

	private void CurrentLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d as CodeWriter).IsSettingValue)
		{
			Selection = new(CurrentLine);
			CenterView();
		}
	}



	private Size MeasureTextSize(CanvasDevice device, string text, CanvasTextFormat textFormat, float limitedToWidth = 0.0f, float limitedToHeight = 0.0f)
	{
		CanvasTextLayout layout = new(device, text, textFormat, limitedToWidth, limitedToHeight);

		double width = layout.DrawBounds.Width;
		double height = layout.DrawBounds.Height;

		return new(width, height);
	}

	private void NormalArrowPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
		//Cursor = new InputCursor(CoreCursorType.Arrow, 1);
	}



	private Point PlaceToPoint(Place currentplace)
	{
		if (Lines?.Count == 0)
			return new Point(0, 0);

		//int y = 0;
		int iline = currentplace.iLine - VisibleLines[0].iLine;
		//int x = 0;
		int ivisualchar = currentplace.iChar;


		int x = (int)(Width_Left + HorizontalOffset + CursorPlace.iChar * CharWidth);
		int y = (int)((currentplace.iLine - VisibleLines[0].iLine) * CharHeight - 1 / 2 * CharHeight);

		for (int i = 0; i < CursorPlace.iChar; i++)
		{
			if (Lines.Count > CursorPlace.iLine)
				if (Lines[CursorPlace.iLine].Count > i)
					if (Lines[CursorPlace.iLine][i].C == '\t')
					{
						x += CharWidth * (TabLength - 1);
					}
		}

		int visualline = 0;
		int ilineoffset = 0;
		int icharoffset = 0;
		int wraplines = 0;
		if (IsWrappingEnabled)
		{
			Action getLine = delegate //() =>
			{
				int iwrappedLines = 0;
				foreach (var line in VisibleLines)
				{
					for (int wrappingLine = 0; wrappingLine < line.WrappedLines.Count; wrappingLine++)
					{

						if (wrappingLine != 0)
							iwrappedLines++;
						if (visualline == iline + iwrappedLines)
						{
							icharoffset = wrappingLine * iVisibleChars;
							wraplines = iwrappedLines;
							return;
						}
						visualline++;
					}
				}
			};
			getLine();
			ilineoffset = wraplines;
		}
		//for (int ivisibleline = 0; ivisibleline < ivisualline; ivisibleline++)
		//{
		//	ilineoffset += VisibleLines[ivisibleline].WrappedLines.Count;

		//}


		//int linewraps = Lines[currentplace.iLine].GetLineWraps(iVisibleChars,TabLength,WrappingLength);

		y += ilineoffset * CharHeight;
		x -= icharoffset * CharWidth;

		//y = (int)(ivisualline * CharHeight);
		//x = (int)(ivisualchar * CharWidth);
		return new Point(x, y);
	}

	private async Task<Place> PointToPlace(Point currentpoint)
	{
		if (VisibleLines?.Count == 0)
			return new Place(0, 0);
		try
		{
			int ivisualline = Math.Max(Math.Min((int)(currentpoint.Y / CharHeight), Lines.Count - 1), 0);
			int iline = 0;
			int visualline = 0;
			int ilineoffset = 0;
			int icharoffset = 0;
			int wraplines = 0;
			var visualLines = VisibleLines.SelectMany(x => x.WrappedLines).ToList();
			if (IsWrappingEnabled)
			{
				Action getLine = delegate //() =>
				{
					foreach (var line in VisibleLines)
					{
						int iwrappedLines = 0;
						for (int wrappingLine = 0; wrappingLine < line.WrappedLines.Count; wrappingLine++)
						{
							if (visualline == ivisualline)
							{
								iline = line.iLine;
								wraplines = iwrappedLines;
								return;
							}
							visualline++;
							iwrappedLines++;
						}
					}
				};
				getLine();
				icharoffset = wraplines > 1 ? wraplines * (iVisibleChars - Lines[iline].Indents * (TabLength - 1)) + (wraplines - 1) * WrappingLength : wraplines * (iVisibleChars - Lines[iline].Indents * (TabLength - 1));
			}
			else
				iline = Math.Max(Math.Min(ivisualline + VisibleLines[0].iLine, Lines.Count - 1), 0);

			int ichar = 0;
			if ((int)currentpoint.X - Width_Left - HorizontalOffset > 0)
			{
				int visualcharplace = (int)((currentpoint.X - Width_Left - HorizontalOffset + CharWidth * 1 / 2) / CharWidth);
				int currentvisibleplace = 0;
				int actualcharplace = visualcharplace;
				for (int i = 0; i < Math.Min(Lines[iline].Count, visualcharplace); i++)
				{
					if (currentvisibleplace < visualcharplace && i < Lines[iline].Count)
						if (Lines[iline][i].C == '\t')
						{
							actualcharplace -= TabLength - 1;
							currentvisibleplace += TabLength;
						}
						else
						{
							currentvisibleplace += 1;
						}
				}
				actualcharplace += icharoffset;
				ichar = Math.Min(actualcharplace, Lines[iline].Count);
			}
			return new Place(ichar, iline);
		}
		catch
		{
			return new Place(0, 0);
		}

	}

	private void RecalcLineNumbers()
	{
		for (int i = CursorPlace.iLine; i < Lines.Count; i++)
			Lines[i].LineNumber = i + 1;
	}





	private CoreVirtualKeyStates shiftKeyState = CoreVirtualKeyStates.None;
	private CoreVirtualKeyStates controlKeyState = CoreVirtualKeyStates.None;

	private void Scroll_KeyUp(object sender, KeyRoutedEventArgs e)
	{
		switch (e.Key)
		{
			case VirtualKey.Control:
				controlKeyState = CoreVirtualKeyStates.None;
				break;
			case VirtualKey.Shift:
				shiftKeyState = CoreVirtualKeyStates.None;
				break;
		}
	}

	private int iCharPosition = 0;

	private void Scroll_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		try
		{
			if (e.Key == VirtualKey.Shift)
			{
				shiftKeyState = CoreVirtualKeyStates.Down;
				e.Handled = true;
				return;
			}
			else if (e.Key == VirtualKey.Control)
			{
				controlKeyState = CoreVirtualKeyStates.Down;
				e.Handled = true;
				return;
			}
			//var shiftkey = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
			bool shiftdown = shiftKeyState == CoreVirtualKeyStates.Down;
			//var controldown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

			if (!isSelecting)
			{
				string storetext = "";
				Place newplace = new(CursorPlace);
				switch (e.Key)
				{
					case VirtualKey.Escape:
						IsSuggesting = false;
						break;

					case VirtualKey.Tab:

						if (IsSuggesting)
						{
							InsertSuggestion();
							CanvasText.Invalidate();
							e.Handled = true;
							break;
						}

						EditActionHistory.Add(new() { TextState = Text, Selection = Selection, TextInvolved = @"\t", EditActionType = EditActionType.Add });

						if (IsSelection)
						{
							Place start = new(Selection.VisualStart);
							Place end = new(Selection.VisualEnd);
							if (shiftKeyState != CoreVirtualKeyStates.None)
							{
								for (int iLine = Selection.VisualStart.iLine; iLine <= Selection.VisualEnd.iLine; iLine++)
								{
									if (Lines[iLine].LineText.StartsWith("\t"))
									{
										Lines[iLine].SetLineText(Lines[iLine].LineText.Remove(0, 1));
										if (iLine == Selection.VisualStart.iLine)
											start -= 1;
										else if (iLine == Selection.VisualEnd.iLine)
											end -= 1;
									}
								}
								Selection = new(start, end);
							}
							else
							{
								for (int iLine = Selection.VisualStart.iLine; iLine <= Selection.VisualEnd.iLine; iLine++)
								{
									Lines[iLine].SetLineText(Lines[iLine].LineText.Insert(0, "\t"));
								}
								Selection = new(Selection.Start + 1, Selection.End + 1);
							}
						}
						else
						{
							if (shiftKeyState == CoreVirtualKeyStates.Down)
							{
								if (Lines[CursorPlace.iLine].LineText.StartsWith("\t"))
								{
									Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Remove(0, 1));
									Selection = new(CursorPlace - 1);
								}
							}
							else
							{
								Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, "\t"));
								Selection = new(CursorPlace + 1);
							}
						}

						updateText();
						CanvasText.Invalidate();
						e.Handled = true;
						break;

					case VirtualKey.Enter:
						if (IsSuggesting)
						{
							if (SuggestionIndex == -1)
							{
								IsSuggesting = false;
							}
							else
							{
								InsertSuggestion();
								CanvasText.Invalidate();
							}
							e.Handled = true;
							break;
						}
						if (controlKeyState == CoreVirtualKeyStates.Down)
						{
							//e.Handled = true;
							break;
						}
						if (IsSelection)
						{
							TextAction_Delete(Selection);
						}
						EditActionHistory.Add(new() { TextState = Text, Selection = new(Selection.VisualStart), TextInvolved = @"\n", EditActionType = EditActionType.Add });
						if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
						{
							storetext = Lines[CursorPlace.iLine].LineText.Substring(CursorPlace.iChar);
							Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar));
						}
						string indents = string.Concat(Enumerable.Repeat("\t", Lines[CursorPlace.iLine].Indents));
						var newline = new Line(Language) { LineNumber = CursorPlace.iLine, IsUnsaved = true };
						newline.SetLineText(indents + storetext);
						Lines.Insert(CursorPlace.iLine + 1, newline);

						for (int i = CursorPlace.iLine + 1; i < Lines.Count; i++)
							Lines[i].LineNumber = i + 1;
						Place newselect = CursorPlace;
						newselect.iLine++;
						newselect.iChar = Lines[CursorPlace.iLine].Indents;
						Selection = new Range(newselect, newselect);
						updateText();
						Invalidate();
						e.Handled = true;
						break;

					case VirtualKey.Delete:
						if (!IsSelection)
						{
							if (CursorPlace.iChar == Lines[CursorPlace.iLine].Count && CursorPlace.iLine < Lines.Count - 1)
							{
								EditActionHistory.Add(new() { TextState = Text, Selection = Selection, EditActionType = EditActionType.Remove, TextInvolved = @"\n" });
								storetext = Lines[CursorPlace.iLine + 1].LineText;
								Lines.RemoveAt(CursorPlace.iLine + 1);
								Lines[CursorPlace.iLine].AddToLineText(storetext);
								updateText();
								Invalidate();
							}
							else if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
							{
								EditActionHistory.Add(new() { TextState = Text, Selection = Selection, EditActionType = EditActionType.Remove, TextInvolved = Lines[CursorPlace.iLine].LineText[CursorPlace.iChar].ToString().Replace("\t", @"\t") });
								Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar, 1));
								CanvasText.Invalidate();
								updateText();

							}
						}
						else
						{
							TextAction_Delete(Selection);
							Selection = new(Selection.VisualStart);
						}
						break;

					case VirtualKey.Back:
						if (!IsSelection)
						{
							if (CursorPlace.iChar == 0 && CursorPlace.iLine > 0)
							{
								if (EditActionHistory.Count > 0)
								{
									EditAction last = EditActionHistory.Last();
									if (last.EditActionType == EditActionType.Remove)
									{
										last.TextInvolved += @"\n";
									}
									else
									{
										EditActionHistory.Add(new() { TextInvolved = @"\n", TextState = Text, EditActionType = EditActionType.Remove, Selection = Selection });
									}
								}
								else
								{
									EditActionHistory.Add(new() { TextInvolved = @"\n", TextState = Text, EditActionType = EditActionType.Remove, Selection = Selection });
								}
								storetext = Lines[CursorPlace.iLine].LineText;
								Lines.RemoveAt(CursorPlace.iLine);
								newplace = new(Lines[CursorPlace.iLine - 1].Count, CursorPlace.iLine - 1);
								Lines[newplace.iLine].AddToLineText(storetext);
								Selection = new Range(newplace);
								RecalcLineNumbers();
								updateText();
								Invalidate();
							}
							else
							{
								if (Language.CommandTriggerCharacters.Contains(Lines[CursorPlace.iLine].LineText[CursorPlace.iChar - 1]))
								{
									IsSuggesting = false;
								}
								string texttoremove = Lines[CursorPlace.iLine].LineText[CursorPlace.iChar - 1].ToString();

								if (EditActionHistory.Count > 0)
								{
									EditAction last = EditActionHistory.Last();
									if (last.EditActionType == EditActionType.Remove)
									{
										last.TextInvolved += texttoremove;
									}
									else
									{
										EditActionHistory.Add(new() { TextInvolved = texttoremove, TextState = Text, EditActionType = EditActionType.Remove, Selection = Selection });
									}
								}
								else
								{
									EditActionHistory.Add(new() { TextInvolved = texttoremove, TextState = Text, EditActionType = EditActionType.Remove, Selection = Selection });
								}

								Lines[CursorPlace.iLine].SetLineText(Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar - 1, 1));

								newplace.iChar--;
								Selection = new Range(newplace);
								FilterSuggestions();
								RecalcLineNumbers();
								updateText();
								CanvasText.Invalidate();
								CanvasLineInfo.Invalidate();
							}



						}
						else
						{
							TextAction_Delete(Selection);
							Selection = new(Selection.VisualStart);
						}
						break;

					case VirtualKey.Home:

						newplace.iChar = newplace.iChar == Lines[newplace.iLine].Indents ? 0 : Lines[newplace.iLine].Indents;
						Selection = new(newplace);
						break;

					case VirtualKey.End:

						newplace.iChar = Lines[newplace.iLine].Count;
						Selection = new(newplace);
						break;

					case VirtualKey.Up:
						if (IsSuggesting)
						{
							if (SuggestionIndex > 0)
								SuggestionIndex--;
							else
								SuggestionIndex = Suggestions.Count - 1;
							break;
						}

						if (CursorPlace.iLine > 0)
						{
							newplace.iLine--;
							newplace.iChar = Math.Min(Lines[newplace.iLine].Count, Math.Max(newplace.iChar, iCharPosition));
							if (shiftdown)
							{
								Selection = new(Selection.Start, newplace);
							}
							else
							{
								Selection = new(newplace);
							}
						}
						break;

					case VirtualKey.Down:
						if (IsSuggesting)
						{
							//Lbx_Suggestions.Focus(FocusState.Keyboard);
							if (SuggestionIndex < Suggestions.Count - 1)
								SuggestionIndex++;
							else
								SuggestionIndex = 0;
							break;
						}

						if (CursorPlace.iLine < Lines.Count - 1)
						{
							newplace.iLine++;
							newplace.iChar = Math.Min(Lines[newplace.iLine].Count, Math.Max(newplace.iChar, iCharPosition));
							if (shiftdown)
							{
								Selection = new(Selection.Start, newplace);
							}
							else
							{
								Selection = new(newplace);
							}
						}
						break;

					case VirtualKey.PageDown:
						newplace.iLine = Math.Min(Lines.Count - 1, CursorPlace.iLine + iVisibleLines);
						newplace.iChar = Math.Min(Lines[newplace.iLine].Count, Math.Max(newplace.iChar, iCharPosition));
						VerticalScroll.Value += (newplace.iLine - CursorPlace.iLine) * CharHeight;
						if (shiftdown)
						{
							Selection = new(Selection.Start, newplace);
						}
						else
						{
							Selection = new(newplace);
						}
						break;

					case VirtualKey.PageUp:
						newplace.iLine = Math.Max(0, CursorPlace.iLine - iVisibleLines);
						newplace.iChar = Math.Min(Lines[newplace.iLine].Count, Math.Max(newplace.iChar, iCharPosition));
						VerticalScroll.Value -= (newplace.iLine - CursorPlace.iLine) * CharHeight;
						if (shiftdown)
						{
							Selection = new(Selection.Start, newplace);
						}
						else
						{
							Selection = new(newplace);
						}
						break;

					case VirtualKey.Left:
						IsSuggesting = false;
						if (CursorPlace.iChar > 0)
						{
							newplace = new(CursorPlace);
							newplace.iChar--;
						}
						else if (CursorPlace.iLine > 0)
						{
							newplace = new(CursorPlace);
							newplace.iLine--;
							newplace.iChar = Lines[newplace.iLine].Count;
						}

						if (shiftdown)
						{
							Selection = new(Selection.Start, newplace);

						}
						else
						{
							Selection = new(newplace);
						}
						iCharPosition = CursorPlace.iChar;
						break;

					case VirtualKey.Right:
						IsSuggesting = false;
						if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
						{
							newplace = new(CursorPlace.iChar, CursorPlace.iLine);
							newplace.iChar++;
						}
						else if (CursorPlace.iLine < Lines.Count - 1)
						{

							newplace.iLine++;
							newplace.iChar = 0;
						}

						if (shiftdown)
						{
							Selection = new(Selection.Start, newplace);
						}
						else
						{
							Selection = new(newplace);
						}
						iCharPosition = CursorPlace.iChar;
						break;
				}
			}

		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
		}
	}

	#region Bindable

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

	#endregion Bindable



	private async void updateText()
	{
		string text = await Task.Run(() =>
		{
			return string.Join("\r\n", Lines.Select(x => x.LineText)); // 6ms
		});

		IsSettingValue = true;
		TextChangedTimerLastText = Text;
		Text = text;
		TextChangedTimer?.Stop();
		TextChangedTimer?.Start();
		TextChanged?.Invoke(this, new(nameof(Text)));
		IsSettingValue = false;
	}

	private async void textChanged()
	{
		try
		{
			if (Lines?.Count >= CursorPlace.iLine + 1)
				if (Lines[CursorPlace.iLine].LineText.Length > maxchars)
				{
					maxchars = Lines[CursorPlace.iLine].LineText.Length;
					HorizontalScroll.Maximum = (maxchars + 2 + Lines[CursorPlace.iLine].Indents * TabLength) * CharWidth - Scroll.ActualWidth + Width_Left;
					VerticalScroll.Visibility = Lines.Count * CharHeight > TextControl.ActualHeight ? Visibility.Visible : Visibility.Collapsed;
					HorizontalScroll.Visibility = maxchars * CharHeight > TextControl.ActualWidth ? Visibility.Visible : Visibility.Collapsed;
				}

			Language lang = Language;

			await Task.Run(() =>
			{
				//DateTime dt1 = DateTime.Now;
				RecalcLineNumbers();
				//DateTime dt2 = DateTime.Now;
				//TimeSpan ddt1 = dt2 - dt1;
				//InfoMessage?.Invoke(this, ddt1.TotalMilliseconds.ToString());
				updateFoldingPairs(lang); // 80 ms
																														//DateTime dt3 = DateTime.Now;
																														//TimeSpan ddt2 = dt3 - dt2;
																														//InfoMessage?.Invoke(this, ddt2.TotalMilliseconds.ToString());

				//DateTime dt4 = DateTime.Now;
				//TimeSpan ddt3 = dt4 - dt3;
				//InfoMessage?.Invoke(this, ddt3.TotalMilliseconds.ToString());


				//DateTime dt5 = DateTime.Now;
				//TimeSpan ddt4 = dt5 - dt4;
				//InfoMessage?.Invoke(this, ddt4.TotalMilliseconds.ToString());

			});

			CanvasLineInfo.Invalidate();

		}
		catch (Exception ex)
		{
			ErrorOccured?.Invoke(this, new(ex));
		}
	}
	bool tempFocus = false;
	bool dragStarted = false;

	PointerPoint CurrentPointerPoint { get; set; }

	private void VerticalScroll_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
	}

	private void Lbx_Suggestions_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		InsertSuggestion();
		CanvasText.Invalidate();
	}

	private void Lbx_Suggestions_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		try
		{
			if (!isSelecting && (e.Key == VirtualKey.Enter | e.Key == VirtualKey.Tab))
			{
				if (IsSuggesting)
				{
					InsertSuggestion();
					CanvasText.Invalidate();
					Focus(FocusState.Keyboard);
				}
				e.Handled = true;
			}
		}
		catch { }
	}

	private void UserControl_GotFocus(object sender, RoutedEventArgs e)
	{
		IsFocused = true;
	}

	private void UserControl_LostFocus(object sender, RoutedEventArgs e)
	{
		IsFocused = false;
		//IsSuggesting = false;
	}

	private async void UserControl_Loaded(object sender, RoutedEventArgs e)
	{
	}


}