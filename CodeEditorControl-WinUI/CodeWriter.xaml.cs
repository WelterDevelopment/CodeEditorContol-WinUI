using CommunityToolkit.WinUI.Helpers;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement.Core;

namespace CodeEditorControl_WinUI
{
	public partial class CodeWriter : UserControl, INotifyPropertyChanged
	{
		public static new readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSize", typeof(int), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).FontSizeChanged(d, e)));
		public static readonly DependencyProperty IsFoldingEnabledProperty = DependencyProperty.Register("IsFoldingEnabled", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
		public static readonly DependencyProperty IsWrappingEnabledProperty = DependencyProperty.Register("IsWrappingEnabled", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
		public static readonly DependencyProperty WrappingLengthProperty = DependencyProperty.Register("WrappingLength", typeof(int), typeof(CodeWriter), new PropertyMetadata(1, (d, e) => ((CodeWriter)d).Invalidate()));
		public static new readonly DependencyProperty LanguageProperty = DependencyProperty.Register("Language", typeof(Language), typeof(CodeWriter), new PropertyMetadata(new Language("ConTeXt"), (d, e) => ((CodeWriter)d).LanguageChanged()));
		public static new readonly DependencyProperty RequestedThemeProperty = DependencyProperty.Register("RequestedTheme", typeof(ElementTheme), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).RequestedThemeChanged(d, e)));
		public static readonly DependencyProperty ScrollPositionProperty = DependencyProperty.Register("ScrollPosition", typeof(Place), typeof(CodeWriter), new PropertyMetadata(new Place(), (d, e) => ((CodeWriter)d).OnScrollPositionChanged(d, e)));
		public static readonly DependencyProperty ShowControlCharactersProperty = DependencyProperty.Register("ShowControlCharacters", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).OnShowControlCharactersChanged(d, e)));
		public static readonly DependencyProperty ShowHorizontalTicksProperty = DependencyProperty.Register("ShowHorizontalTicks", typeof(bool), typeof(CodeWriter), new PropertyMetadata(false, (d, e) => ((CodeWriter)d).Invalidate()));
		public static readonly DependencyProperty ShowLineMarkersProperty = DependencyProperty.Register("ShowLineMarkers", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
		public static readonly DependencyProperty ShowLineNumbersProperty = DependencyProperty.Register("ShowLineNumbers", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
		public static readonly DependencyProperty ShowScrollbarMarkersProperty = DependencyProperty.Register("ShowScrollbarMarkers", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
		public static readonly DependencyProperty ShowScrollbarsProperty = DependencyProperty.Register("ShowScrollbars", typeof(bool), typeof(CodeWriter), new PropertyMetadata(false, (d, e) => { ((CodeWriter)d).OnShowScrollbarsChanged(d, e); }));
		public static readonly DependencyProperty TabLengthProperty = DependencyProperty.Register("TabLength", typeof(int), typeof(CodeWriter), new PropertyMetadata(2, (d, e) => ((CodeWriter)d).OnTabLengthChanged(d, e)));
		public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(CodeWriter), new PropertyMetadata(null, (d, e) => ((CodeWriter)d).OnTextChanged(d, e)));
		public static readonly DependencyProperty CurrentLineProperty = DependencyProperty.Register("CurrentLine", typeof(Place), typeof(CodeWriter), new PropertyMetadata(new Place(0, 0), (d, e) => ((CodeWriter)d).CurrentLineChanged(d, e)));

		public static CodeWriter Current;

		private new ElementTheme ActualTheme = ElementTheme.Dark;
		private List<Place> CursorPlaceHistory = new();
		public ObservableCollection<EditAction> EditActionHistory { get => Get(new ObservableCollection<EditAction>()); set => Set(value); }
		public ObservableCollection<EditAction> InvertedEditActionHistory { get => Get(new ObservableCollection<EditAction>()); set => Set(value); }
		private int iCharOffset = 0;
		private int iVisibleChars { get => (int)(((int)TextControl.ActualWidth - Width_Left) / CharWidth); }
		private bool invoked = false;

		private bool isDragging = false;
		private string draggedText = "";
		private Range draggedSelection;

		private bool IsSettingValue = false;

		private int maxchars = 0;
		public bool IsInitialized = false;

		private int MaxFontSize = 64;

		private Point middleClickScrollingEndPoint = new Point();

		private Point middleClickScrollingStartPoint = new Point();

		private int MinFontSize = 6;

		private Point previousPosition = new Point() { };

		private int searchindex = 0;

		private int startFontsize = 16;

		private float scale
		{
			get
			{
				return XamlRoot != null && XamlRoot?.RasterizationScale != null ? (float)XamlRoot.RasterizationScale : 1.0f;
			}
		}

		private Place SuggestionStart = new Place();

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

			EditActionHistory.CollectionChanged += EditActionHistory_CollectionChanged;
			Current = this;
		}

		public bool CanUndo { get => Get(false); set => Set(value); }
		public bool CanToggleComment { get => Get(false); set => Set(value); }
		public bool CanRedo { get => Get(false); set => Set(value); }
		private void EditActionHistory_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			CanUndo = EditActionHistory.Count > 0;
			InvertedEditActionHistory = new(EditActionHistory.Reverse());
		}

		public event ErrorEventHandler ErrorOccured;

		public event EventHandler<string> InfoMessage;
		public event EventHandler DoubleClicked;

		public event PropertyChangedEventHandler TextChanged;
		public event PropertyChangedEventHandler LinesChanged;
		public event PropertyChangedEventHandler CursorPlaceChanged;

		public event EventHandler Initialized;

		public enum ScrollOrientation
		{
			VerticalScroll,
			HorizontalScroll
		}

		//public new SolidColorBrush Background { get => Get(new SolidColorBrush(Color.FromArgb(255, 10, 10, 10))); set => Set(value); }

		public int CharHeight { get => Get(16); set { Set(value); } }

		public int CharWidth { get => Get(8); set { Set(value); } }

		public Color Color_Beam { get => Get(Color.FromArgb(255, 200, 200, 200)); set => Set(value); }

		public Color Color_FoldingMarker { get => Get(Color.FromArgb(255, 140, 140, 140)); set => Set(value); }

		public Color Color_LeftBackground { get => Get(Color.FromArgb(255, 40, 40, 40)); set => Set(value); }

		public Color Color_LineNumber { get => Get(Color.FromArgb(255, 120, 160, 180)); set => Set(value); }

		public Color Color_Selection { get => Get(Color.FromArgb(255, 25, 50, 80)); set => Set(value); }

		public Color Color_UnsavedMarker { get => Get(Color.FromArgb(255, 80, 190, 230)); set => Set(value); }

		public Color Color_WeakMarker { get => Get(Color.FromArgb(255, 60, 60, 60)); set => Set(value); }

		public ICommand Command_Copy { get; set; }

		public ICommand Command_Cut { get; set; }

		public ICommand Command_Delete { get; set; }

		public ICommand Command_Find { get; set; }

		public ICommand Command_Paste { get; set; }

		public ICommand Command_SelectAll { get; set; }
		public ICommand Command_Undo { get; set; }
		public ICommand Command_ToggleComment { get; set; }

		//public CoreCursor Cursor
		//{
		//	get { return base.curs; }
		//	set { base.ProtectedCursor = value; }
		//}

		public Place CursorPlace
		{
			get => Get(new Place());
			set
			{
				Set(value);
				if (isCanvasLoaded)
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

		public new int FontSize { get => (int)GetValue(FontSizeProperty); set { SetValue(FontSizeProperty, value); } }
		public int ScaledFontSize { get => (int)((float)FontSize * scale); }

		public Place CurrentLine { get => (Place)GetValue(CurrentLineProperty); set { SetValue(CurrentLineProperty, value); } }
		public int HorizontalOffset { get => Get(0); set { Set(value); } }

		public bool IsFindPopupOpen { get => Get(false); set { Set(value); if (!value) { Tbx_Search.Text = ""; TextControl.Focus(FocusState.Keyboard); } } }

		public bool IsFoldingEnabled { get => (bool)GetValue(IsFoldingEnabledProperty); set { SetValue(IsFoldingEnabledProperty, value); } }
		public bool IsWrappingEnabled { get => (bool)GetValue(IsWrappingEnabledProperty); set { SetValue(IsWrappingEnabledProperty, value); } }

		public bool ShowScrollbars
		{
			get => (bool)GetValue(ShowScrollbarsProperty); set => SetValue(ShowScrollbarsProperty, value);
		}

		private void OnShowScrollbarsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if ((bool)e.NewValue)
				HorizontalScroll.Style = VerticalScroll.Style = Resources["AlwaysExpandedScrollBar"] as Style;
			else
			{
				HorizontalScroll.Style = VerticalScroll.Style = Application.Current.Resources["DefaultScrollBarStyle"] as Style;
			}
		}


		public int WrappingLength { get => (int)GetValue(WrappingLengthProperty); set { SetValue(WrappingLengthProperty, value); } }

		public bool IsMatchCase { get => Get(false); set { Set(value); Tbx_SearchChanged(null, null); } }

		public bool IsRegex { get => Get(false); set { Set(value); Tbx_SearchChanged(null, null); } }

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

		public new Language Language
		{
			get =>
				(Language)GetValue(LanguageProperty);
			set
			{
				SetValue(LanguageProperty, value);
			}
		}

		public ObservableCollection<Line> Lines { get => Get(new ObservableCollection<Line>()); set => Set(value); }

		public new ElementTheme RequestedTheme
		{
			get => (ElementTheme)GetValue(RequestedThemeProperty);
			set => SetValue(RequestedThemeProperty, value);
		}

		public Place ScrollPosition
		{
			get => (Place)GetValue(ScrollPositionProperty);
			set => SetValue(ScrollPositionProperty, value);
		}

		public List<SearchMatch> SearchMatches { get => Get(new List<SearchMatch>()); set { Set(value); CanvasScrollbarMarkers.Invalidate(); } }

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
							text += Lines[iLine].LineText.Substring(start.iChar) + "\n";
						else if (iLine == end.iLine)
							text += Lines[iLine].LineText.Substring(0, end.iChar);
						else
							text += Lines[iLine].LineText + "\n";
					}
				}

				return text;
			}
		}

		public List<Line> SelectedLines = new();

		public Range Selection
		{
			get => Get(new Range());
			set
			{
				Set(value);
				CursorPlace = new Place(value.End.iChar, value.End.iLine);
				IsSelection = value.Start != value.End;
				SelectedLines = Lines.Where(x => x.iLine >= value.VisualStart.iLine && x.iLine <= value.VisualEnd.iLine).ToList();
				CanvasSelection.Invalidate();
			}
		}

		public bool ShowControlCharacters
		{
			get => (bool)GetValue(ShowControlCharactersProperty);
			set => SetValue(ShowControlCharactersProperty, value);
		}

		public bool ShowHorizontalTicks
		{
			get => (bool)GetValue(ShowHorizontalTicksProperty);
			set { SetValue(ShowHorizontalTicksProperty, value); }
		}

		public bool ShowLineMarkers
		{
			get => (bool)GetValue(ShowLineMarkersProperty);
			set { SetValue(ShowLineMarkersProperty, value); }
		}

		public bool ShowLineNumbers
		{
			get => (bool)GetValue(ShowLineNumbersProperty);
			set
			{
				SetValue(ShowLineNumbersProperty, value);
			}
		}

		public bool ShowScrollbarMarkers
		{
			get => (bool)GetValue(ShowScrollbarMarkersProperty);
			set { SetValue(ShowScrollbarMarkersProperty, value); }
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

		public int TabLength
		{
			get => (int)GetValue(TabLengthProperty);
			set => SetValue(TabLengthProperty, value);
		}

		public string Text
		{
			get => (string)GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		public List<Line> VisibleLines { get; set; } = new List<Line>();

		public int Width_Left { get => (int)Width_LeftMargin + (int)Width_LineNumber + (int)Width_ErrorMarker + (int)Width_WarningMarker + (int)Width_FoldingMarker + Width_TextIndent; }

		public int Width_LineNumber { get => Get(12); set => Set(value); }
		public int Width_LeftMargin { get => Get(6); set => Set(value); }

		public bool WordWrap { get; private set; } = true;

		private List<Suggestion> Commands
		{
			get => Get(new List<Suggestion>() {
						new IntelliSense(@"\foo"){ IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
						new IntelliSense(@"\bar"){ IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
						new IntelliSense(@"\foobar"){ IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
				}); set => Set(value);
		}

		private bool isCanvasLoaded { get => CanvasText.IsLoaded; }

		private bool isLineSelect { get; set; } = false;

		private bool isMiddleClickScrolling { get => Get(false); set { Set(value); } }
		private bool IsFocused { get => Get(false); set { Set(value); } }

		private bool isSelecting { get; set; } = false;

		private List<Parameter> Options
		{
			get => Get(new List<Parameter>()); set => Set(value);
		}

		private float scrollbarSize { get => (float)Application.Current.Resources["ScrollBarSize"]; }

		private int SuggestionIndex { get => Get(-1); set { Set(value); if (value == -1) SelectedSuggestion = null; else if (Suggestions?.Count > value) { SelectedSuggestion = Suggestions[value]; Lbx_Suggestions.ScrollIntoView(SelectedSuggestion); } } }
		private Suggestion SelectedSuggestion { get => Get<Suggestion>(); set => Set(value); }

		private List<Suggestion> AllOptions { get => Get<List<Suggestion>>(); set => Set(value); }
		private List<Suggestion> AllSuggestions { get => Get(Commands); set => Set(value); }
		private List<Suggestion> Suggestions { get => Get(Commands); set => Set(value); }

		public void UpdateSuggestions()
		{
			AllSuggestions = Language.Commands;
			Suggestions = Language.Commands;
		}

		private int Width_ErrorMarker { get; set; } = 12;

		private int Width_FoldingMarker { get; set; } = 24;

		private int Width_TextIndent { get => CharWidth / 2; }

		private int Width_WarningMarker { get; set; } = 12;

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

		public void Action_Add(MenuFlyoutItemBase item)
		{
			ContextMenu.Items.Add(item);
		}

		public void Action_Remove(MenuFlyoutItemBase item)
		{
			ContextMenu.Items.Remove(item);
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

		public void Invalidate(bool sizechanged = true)
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

		public void TextAction_SelectText(Range range = null)
		{
			if (range == null && Lines.Count > 0)
			{
				Selection = new Range(new(0, 0), new(Lines.Last().Count, Lines.Count - 1));
			}
		}

		private void Btn_SearchClose(object sender, RoutedEventArgs e)
		{
			IsFindPopupOpen = false;
		}

		private void Btn_SearchNext(object sender, RoutedEventArgs e)
		{
			try
			{
				searchindex++;
				if (searchindex >= SearchMatches.Count)
				{
					searchindex = 0;
				}

				if (SearchMatches.Count > 0)
				{
					SearchMatch sm = SearchMatches[searchindex];
					Selection = new(new Place(sm.iChar, sm.iLine));
				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}

		private void Btn_ReplaceNext(object sender, RoutedEventArgs e)
		{
			try
			{
				if (SearchMatches.Count > 0)
				{
					SearchMatch sm = SearchMatches[searchindex];
					Lines[sm.iLine].LineText = Lines[sm.iLine].LineText.Remove(sm.iChar, sm.Match.Length).Insert(sm.iChar, Tbx_Replace.Text);
					SearchMatches.RemoveAt(searchindex);
					textChanged();
					Invalidate();
					Selection = new(new(sm.iChar, sm.iLine), new(sm.iChar + Tbx_Replace.Text.Length, sm.iLine));

				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}

		private void Btn_ReplaceAll(object sender, RoutedEventArgs e)
		{
			try
			{
				if (SearchMatches.Count > 0)
				{
					foreach (SearchMatch sm in SearchMatches)
					{
						Lines[sm.iLine].LineText = Lines[sm.iLine].LineText.Remove(sm.iChar, sm.Match.Length).Insert(sm.iChar, Tbx_Replace.Text);
					}
					Selection = new(new(SearchMatches.Last().iChar, SearchMatches.Last().iLine), new(SearchMatches.Last().iChar + Tbx_Replace.Text.Length, SearchMatches.Last().iLine));
					SearchMatches.Clear();
					textChanged();
					Invalidate();
				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
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
						args.DrawingSession.DrawRoundedRectangle(Width_Left, y, (int)TextControl.ActualWidth - Width_Left, CharHeight, 2, 2, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2f);
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

					foreach (SearchMatch search in new List<SearchMatch>(SearchMatches))
						args.DrawingSession.DrawLine(width / 3f, search.iLine / (float)Lines.Count * height, width * 2 / 3f, search.iLine / (float)Lines.Count * height, ActualTheme == ElementTheme.Light ? Colors.LightGray.ChangeColorBrightness(-0.3f) : Colors.LightGray, markersize);

					foreach (Line line in Lines.Where(x => x.IsUnsaved))
						args.DrawingSession.DrawLine(0, line.iLine / (float)Lines.Count * height, width * 1 / 3f, line.iLine / (float)Lines.Count * height, ActualTheme == ElementTheme.Light ? Color_UnsavedMarker.ChangeColorBrightness(-0.2f) : Color_UnsavedMarker, markersize);

					foreach (SyntaxError error in new List<SyntaxError>(SyntaxErrors))
					{
						if (error.SyntaxErrorType == SyntaxErrorType.Error)
						{
							args.DrawingSession.DrawLine(width * 2 / 3f, error.iLine / (float)Lines.Count * height, width, error.iLine / (float)Lines.Count * height, ActualTheme == ElementTheme.Light ? Colors.Red.ChangeColorBrightness(-0.2f) : Colors.Red, markersize);
						}
						else if (error.SyntaxErrorType == SyntaxErrorType.Warning)
						{
							args.DrawingSession.DrawLine(width * 2 / 3f, error.iLine / (float)Lines.Count * height, width, error.iLine / (float)Lines.Count * height, ActualTheme == ElementTheme.Light ? Colors.Yellow.ChangeColorBrightness(-0.2f) : Colors.Yellow, markersize);
						}
					}

					float cursorY = CursorPlace.iLine / (float)Lines.Count * height;
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

		private void CanvasText_Draw(CanvasControl sender, CanvasDrawEventArgs args)
		{
			try
			{
				sender.DpiScale = XamlRoot.RasterizationScale > 1.0d ? 1.15f : 1.0f; // The text was shaking around on text input at Scale factors > 1. Setting DpiScale seems to prevent this.
																																																																									//args.DrawingSession.Antialiasing = CanvasAntialiasing.Antialiased;
																																																																									//args.DrawingSession.Blend = CanvasBlend.Add;
																																																																									//args.DrawingSession.TextAntialiasing = CanvasTextAntialiasing.ClearType;
				if (VisibleLines.Count > 0)
				{
					int foldPos = Width_LeftMargin + Width_LineNumber + Width_ErrorMarker + Width_WarningMarker;
					int errorPos = Width_LeftMargin + Width_LineNumber;
					int warningPos = errorPos + Width_ErrorMarker;
					int totalwraps = 0;

					for (int iLine = VisibleLines[0].iLine; iLine < VisibleLines.Last().LineNumber; iLine++)
					{
						int y = CharHeight * (iLine - VisibleLines[0].LineNumber + 1 + totalwraps);
						int x = 0;
						args.DrawingSession.FillRectangle(0, y, Width_Left - Width_TextIndent, CharHeight, Color_LeftBackground);

						if (ShowLineNumbers)
							args.DrawingSession.DrawText((iLine + 1).ToString(), CharWidth * IntLength(Lines.Count) + Width_LeftMargin, y, ActualTheme == ElementTheme.Light ? Color_LineNumber.InvertColorBrightness() : Color_LineNumber, new CanvasTextFormat() { FontFamily = "Consolas", FontSize = ScaledFontSize, HorizontalAlignment = CanvasHorizontalAlignment.Right });
						if (IsFoldingEnabled)
							if (Lines[iLine].IsFoldStart)
							{
								args.DrawingSession.DrawRectangle(foldPos, y + CharHeight / 4, CharWidth, CharWidth, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
								args.DrawingSession.DrawLine(foldPos + CharWidth / 4, y + CharHeight / 2, foldPos + CharWidth * 3 / 4, y + CharHeight / 2, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
							}
							else if (Lines[iLine].IsFoldInner)
							{
								args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y, foldPos + CharWidth / 2, y + CharHeight, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
							}
							else if (Lines[iLine].IsFoldInnerEnd)
							{
								args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y, foldPos + CharWidth / 2, y + CharHeight, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
								args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y + CharHeight / 2, foldPos + CharWidth, y + CharHeight / 2, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
							}

						if (ShowLineMarkers)
						{
							if (Lines[iLine].IsUnsaved)
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

						int lastChar = IsWrappingEnabled ? Lines[iLine].Count : Math.Min(iCharOffset + ((int)Scroll.ActualWidth - Width_Left) / CharWidth, Lines[iLine].Count);
						int indents = 0;

						int textWrappingLines = Lines[iLine].Count / ((int)Scroll.ActualWidth - Width_Left);
						int linewraps = 0;
						int wrapindent = 0;
						int iWrappingChar = 0;

						if (IsWrappingEnabled)
						{
							for (int iWrappedLine = 0; iWrappedLine < Lines[iLine].WrappedLines.Count; iWrappedLine++)
							{
								var wrappedLine = Lines[iLine].WrappedLines[iWrappedLine];
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

										args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[c.T].InvertColorBrightness() : EditorOptions.TokenColors[c.T], new CanvasTextFormat() { FontFamily = "Consolas", FontSize = ScaledFontSize });


									}

								}
								if (iWrappedLine < Lines[iLine].WrappedLines.Count - 1)
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
								Char c = Lines[iLine][iChar];

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

											args.DrawingSession.DrawGeometry(arrow, x, y, ActualTheme == ElementTheme.Light ? Color_WeakMarker.InvertColorBrightness() : Color_WeakMarker, 2);
										}
								}
								else if (iChar >= iCharOffset - indents * (TabLength - 1))
								{
									if (!IsWrappingEnabled && iChar < iCharOffset - indents * (TabLength - 1) + iVisibleChars)
									{
										x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
										args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[c.T].InvertColorBrightness() : EditorOptions.TokenColors[c.T], CanvasTextFormat);
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

										args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? EditorOptions.TokenColors[c.T].InvertColorBrightness() : EditorOptions.TokenColors[c.T], new CanvasTextFormat() { FontFamily = "Consolas", FontSize = ScaledFontSize });
									}

								}
							}
						if (ShowControlCharacters && iLine < Lines.Count - 1 && lastChar >= iCharOffset - indents * (TabLength - 1))
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

							args.DrawingSession.DrawGeometry(enter, x, y, ActualTheme == ElementTheme.Light ? Color_WeakMarker.InvertColorBrightness() : Color_WeakMarker, 2);
						}
					}
				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}

		private void CodeWriter_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
		{
			try
			{
				if (isSelecting) return;
				if (char.IsLetterOrDigit(args.Character) | char.IsSymbol(args.Character) | char.IsPunctuation(args.Character) | char.IsSeparator(args.Character) | char.IsSurrogate(args.Character))
				{
					if (IsSelection)
					{
						TextAction_Delete(Selection);
						CursorPlace = Selection.VisualStart;
					}

					if (Language.CommandTriggerCharacters.Contains(args.Character))
					{
						SuggestionStart = CursorPlace;
						//Suggestions = Commands;
						SuggestionIndex = -1;

						IsSuggesting = true;
						IsSuggestingOptions = false;
						Lbx_Suggestions.ScrollIntoView(Suggestions.FirstOrDefault());
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

					Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, args.Character.ToString());


					if (((args.Character == ',' | args.Character == ' ') && IsInsideBrackets(CursorPlace)) | Language.OptionsTriggerCharacters.Contains(args.Character))
					{
						IsSuggestingOptions = true;
						SuggestionStart = CursorPlace;
						var command = GetCommandAtPosition(CursorPlace);
						IntelliSense intelliSense = command.Command;
						var argument = command.ArgumentsRanges.FirstOrDefault(x => CursorPlace.iChar >= x.Start.iChar && CursorPlace.iChar <= x.End.iChar);
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
									AllOptions = Suggestions = Options.Select(x => { if (x is KeyValue) ((Suggestion)x).Snippet = "="; return (Suggestion)x; }).ToList();
									IsSuggesting = true;
								}
							}
						}
					}

					if (Language.AutoClosingPairs.Keys.Contains(args.Character))
					{
						if (CursorPlace.iChar == Lines[CursorPlace.iLine].Count - 1)
							Lines[CursorPlace.iLine].LineText += Language.AutoClosingPairs[args.Character];
						else if (Lines[CursorPlace.iLine].LineText[CursorPlace.iChar + 1] == ' ')
							Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar + 1, Language.AutoClosingPairs[args.Character].ToString());
					}

					Selection = new(Selection.VisualStart + 1);
					FilterSuggestions();
					textChanged();
					CanvasText.Invalidate();
					IsFindPopupOpen = false;
					args.Handled = true;
				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}

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
				color = Colors.DeepSkyBlue;
			}
			session.FillRectangle(x, y, CharWidth * w + 1, CharHeight + 1, ActualTheme == ElementTheme.Light ? color.InvertColorBrightness() : color);
		}

		private async void DrawText(bool sizechanged = false)
		{
			if (sizechanged)
			{
				CanvasTextFormat = new CanvasTextFormat
				{
					FontFamily = "Consolas",
					FontSize = ScaledFontSize
				};
				Size size = MeasureTextSize(CanvasDevice.GetSharedDevice(), "M", CanvasTextFormat);
				Size sizew = MeasureTextSize(CanvasDevice.GetSharedDevice(), "M", CanvasTextFormat);
				CharHeight = (int)(size.Height * 2f);
				CharWidth = (int)(sizew.Width * 1.1f);
			}


			if (VerticalScroll != null && HorizontalScroll != null && Lines != null)
			{
				int StartLine = (int)(VerticalScroll.Value / CharHeight) + 1;
				int EndLine = Math.Min((int)((VerticalScroll.Value + Scroll.ActualHeight) / CharHeight) + 1, Lines.Count);

				maxchars = 0;
				foreach (Line l in new List<Line>(Lines))
				{
					maxchars = Math.Max(l.Count + l.Indents * (TabLength - 1) + 1, maxchars);
				}

				VisibleLines.Clear();
				foreach (Line l in new List<Line>(Lines))
				{
					if (l.LineNumber <= EndLine && l.LineNumber >= StartLine)
					{
						VisibleLines.Add(l);
					}
				}

				//iVisibleChars = (int)(((int)TextControl.ActualWidth - Width_Left) / CharWidth);

				Width_LeftMargin = ShowLineNumbers ? CharWidth : 0;
				Width_LineNumber = ShowLineNumbers ? CharWidth * IntLength(Lines.Count) : 0;
				Width_FoldingMarker = IsFoldingEnabled ? CharWidth : 0;
				Width_ErrorMarker = CharWidth / 2;
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


				DispatcherQueue.TryEnqueue(async () =>
				{
					//await Task.Run(()=> // Doesn't work because somewhere in CalculateLineWraps() the UI thread is called.
					CalculateLineWraps()
					//)
					;

					CanvasBeam.Invalidate();
					CanvasSelection.Invalidate();
					CanvasText.Invalidate();
					CanvasScrollbarMarkers.Invalidate();
				});
			}
		}

		private void CalculateLineWraps()
		{
			var lines = new List<Line>(Lines);
			foreach (Line line in lines)
			{
				line.CalculateWrappedLines(iVisibleChars, TabLength, WrappingLength);
			}
		}

		private CanvasTextFormat CanvasTextFormat { get; set; } = new CanvasTextFormat();
		private void FontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			int currline = 0;

			if (VerticalScroll != null)
			{
				currline = (int)VerticalScroll.Value / CharHeight;
			}
			Invalidate(true);
			if (VerticalScroll != null)
			{
				VerticalScroll.Value = currline * CharHeight;
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

		private async Task InitializeLines(string text)
		{
			VisibleLines.Clear();
			Lines.Clear();



			Language lang = Language;
			string name = lang.Name;

			int lastVisibleLine = (int)CanvasText.ActualHeight / CharHeight;

			if (text == null) return;

			await Task.Run(() =>
			{
				string[] lines = text.Contains("\r\n") ? text.Split("\r\n", StringSplitOptions.None) : text.Split("\n", StringSplitOptions.None);

				lastVisibleLine = Math.Min(Lines.Count, lastVisibleLine + 5);
				int lineNumber = 1;
				bool innerLang = false;
				foreach (string line in lines)
				{
					Language lg = lang;

					if (line.Contains("\\stoplua") | line.Contains("\\stopluacode"))
					{
						innerLang = false;
					}

					if (innerLang)
					{
						lg = Languages.Lua;
					}
					else
					{
						lg = lang;
					}

					if (line.Contains("\\startlua") | line.Contains("\\startluacode"))
					{
						innerLang = true;
					}


					Line l = new Line(lg) { LineNumber = lineNumber, LineText = line };
					l.Save();
					Lines.Add(l);
					lineNumber++;

					if (lineNumber == lastVisibleLine && lines.Length > 50)
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
									Range newSelection = new(Selection.VisualEnd);

									if (newSelection.VisualEnd.iLine > Lines.Count)
										newSelection = new(new Place(Lines.Last().Count - 1, Lines.Count - 1));
									else if (newSelection.VisualEnd.iChar > Lines[newSelection.VisualEnd.iLine].Count)
										newSelection = new(new Place(Lines[newSelection.VisualEnd.iLine].Count, newSelection.VisualEnd.iLine));

									Selection = newSelection;
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

		class CommandAtPosition
		{
			public IntelliSense Command { get; set; }
			public Range CommandRange { get; set; }
			public List<Range> ArgumentsRanges { get; set; }
		}

		private CommandAtPosition GetCommandAtPosition(Place place)
		{
			CommandAtPosition commandAtPosition = new CommandAtPosition();
			MatchCollection commandsInLine = Regex.Matches(Lines[place.iLine].LineText, @"(\\.+?\b)(\[(?>\[(?<c>)|[^\[\]]+|\](?<-c>))*(?(c)(?!))\])*");

			if (commandsInLine.Any())
			{
				foreach (Match command in commandsInLine)
				{
					if (command.Success && place.iChar>= command.Index && place.iChar <= command.Index + command.Length)
					{
						var commandname = command.Groups[1];
						commandAtPosition.CommandRange = new(new(commandname.Index,place.iLine),new(command.Index+command.Length,place.iLine));
						commandAtPosition.Command = AllSuggestions.FirstOrDefault(x=>x.Name == commandname.Value) as IntelliSense;
						if (command.Groups.Count > 2) {
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

		private void CurrentLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (!(d as CodeWriter).IsSettingValue)
			{
				Selection = new(CurrentLine);
				CenterView();
			}
		}

		private async void LanguageChanged()
		{
			//Language lang = Language ?? Languages.ConTeXt;
			//await Task.Run(
			//		() =>
			//{
			CanToggleComment = !string.IsNullOrEmpty(Language.LineComment);
			bool innerLang = false;
			foreach (Line line in Lines)
			{
				if (innerLang)
				{
					line.Language = Languages.Lua;
				}
				else
				{
					line.Language = Language;
				}

				if (line.LineText.Contains("\\startlua") | line.LineText.Contains("\\startluacode"))
				{
					innerLang = true;
				}
			}
			DispatcherQueue.TryEnqueue(() => { Invalidate(); });
			//});
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

		private void OnScrollPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
		}

		private void OnShowControlCharactersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CanvasText.Invalidate();
		}

		private void OnTabLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Invalidate();
		}

		private async void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (!(d as CodeWriter).IsSettingValue)
			{
				while (!CanvasText.IsLoaded | !CanvasBeam.IsLoaded | !CanvasSelection.IsLoaded | !CanvasScrollbarMarkers.IsLoaded) // ToDo: Very ugly workaround, logic needs to be overthought
					await Task.Delay(10);
				await InitializeLines((string)e.NewValue);
			}
			TextChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
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
								icharoffset = wrappingLine*iVisibleChars;
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
					iline = ivisualline + VisibleLines[0].iLine;

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

		private void RequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			try
			{
				ActualTheme = (ElementTheme)e.NewValue;

				if ((ElementTheme)e.NewValue == ElementTheme.Default)
				{
					var backcolor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
					ActualTheme = backcolor.Equals(Colors.White) ? ElementTheme.Light : ElementTheme.Dark;
				}

				if (ActualTheme == ElementTheme.Light)
				{
					Background = new SolidColorBrush(Color.FromArgb(255, 240, 240, 240));
					Color_LeftBackground = Color.FromArgb(255, 220, 220, 220);
					//Color_LineNumber = Color.FromArgb(255, 120, 160, 180).InvertColorBrightness;
				}
				else
				{
					Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30));
					Color_LeftBackground = Color.FromArgb(255, 25, 25, 25);
					//Color_LineNumber = Color.FromArgb(255, 120, 160, 180);
				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
			Invalidate();
		}

		private void insertSuggestion()
		{
			TextControl.Focus(FocusState.Keyboard);

			if (Suggestions[SuggestionIndex].IntelliSenseType == IntelliSenseType.Command)
			{
				Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(SuggestionStart.iChar, CursorPlace.iChar - SuggestionStart.iChar);

				EditActionHistory.Remove(EditActionHistory.LastOrDefault());
				//EditActionHistory.Add(new() { TextState = Text, Selection = Selection, TextInvolved = Suggestions[SuggestionIndex].Name, EditActionType = EditActionType.Paste });
				TextAction_Paste(Suggestions[SuggestionIndex].Name + Suggestions[SuggestionIndex].Snippet, SuggestionStart);

				Selection = new(new Place(SuggestionStart.iChar + Suggestions[SuggestionIndex].Name.Length, CursorPlace.iLine));
			}
			else
			{
				EditActionHistory.Add(new() { TextState = Text, Selection = Selection, TextInvolved = Suggestions[SuggestionIndex].Name, EditActionType = EditActionType.Paste });
				Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(SuggestionStart.iChar + 1, CursorPlace.iChar - (SuggestionStart.iChar + 1));
				Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(SuggestionStart.iChar + 1, Suggestions[SuggestionIndex].Name);
				Selection = new(new Place(SuggestionStart.iChar + 1 + Suggestions[SuggestionIndex].Name.Length, CursorPlace.iLine));
			}

			IsSuggesting = false;

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
				//var controlkey = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

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
								insertSuggestion();
								textChanged();
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
											Lines[iLine].LineText = Lines[iLine].LineText.Remove(0, 1);
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
										Lines[iLine].LineText = Lines[iLine].LineText.Insert(0, "\t");
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
										Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(0, 1);
										Selection = new(CursorPlace - 1);
									}
								}
								else
								{
									Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, "\t");
									Selection = new(CursorPlace + 1);
								}
							}

							textChanged();
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
									insertSuggestion();
									textChanged();
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
								Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar);
							}
							string indents = string.Concat(Enumerable.Repeat("\t", Lines[CursorPlace.iLine].Indents));
							Lines.Insert(CursorPlace.iLine + 1, new Line(Language) { LineNumber = CursorPlace.iLine, LineText = indents + storetext, IsUnsaved = true });
							for (int i = CursorPlace.iLine + 1; i < Lines.Count; i++)
								Lines[i].LineNumber = i + 1;
							Place newselect = CursorPlace;
							newselect.iLine++;
							newselect.iChar = Lines[CursorPlace.iLine].Indents;
							Selection = new Range(newselect, newselect);
							textChanged();
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
									Lines[CursorPlace.iLine].LineText += storetext;
									textChanged();
									Invalidate();
								}
								else if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
								{
									EditActionHistory.Add(new() { TextState = Text, Selection = Selection, EditActionType = EditActionType.Remove, TextInvolved = Lines[CursorPlace.iLine].LineText[CursorPlace.iChar].ToString().Replace("\t", @"\t") });
									Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar, 1);
									textChanged();
									CanvasText.Invalidate();
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
									Lines[newplace.iLine].LineText += storetext;
									Selection = new Range(newplace);

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

									Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar - 1, 1);

									newplace.iChar--;
									Selection = new Range(newplace);
								}
								FilterSuggestions();
								textChanged();
								Invalidate();
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
								newplace.iChar = Math.Min(Lines[newplace.iLine].Count, newplace.iChar);
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
								newplace.iChar = Math.Min(Lines[newplace.iLine].Count, newplace.iChar);
								if (shiftdown)
								{
									Selection = new(Selection.Start, newplace);
								}
								else
								{
									Selection = new(newplace);
								}
								break;
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
							break;
					}
				}

			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
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
						if (pointer.Properties.MouseWheelDelta % 120 == 0) // Mouse
						{
							HorizontalScroll.Value += 6 * pointer.Properties.MouseWheelDelta / 120 * CharWidth;
						}
						else // Trackpad
						{
							HorizontalScroll.Value += pointer.Properties.MouseWheelDelta;
						}
					}
					else if (e.KeyModifiers == VirtualKeyModifiers.Shift)
					{
						if (pointer.Properties.MouseWheelDelta % 120 == 0) // Mouse
						{
							HorizontalScroll.Value -= 3 * pointer.Properties.MouseWheelDelta / 120 * CharWidth;
						}
						else // Trackpad
						{
							HorizontalScroll.Value -= pointer.Properties.MouseWheelDelta;
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

		private void Tbx_Search_KeyDown(object sender, KeyRoutedEventArgs e)
		{
			try
			{
				switch (e.Key)
				{
					case VirtualKey.Enter:
						Btn_SearchNext(null, null);
						e.Handled = true;
						break;
					case VirtualKey.Escape:
						IsFindPopupOpen = false;
						e.Handled = true;
						break;
					case VirtualKey.Tab:
						Tbx_Replace.Focus(FocusState.Keyboard);
						e.Handled = true;
						break;
					case VirtualKey.Back:
						e.Handled = true;
						break;
				}

			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}

		private void Tbx_Replace_KeyDown(object sender, KeyRoutedEventArgs e)
		{
			try
			{
				if (e.Key == VirtualKey.Enter)
				{
					Btn_ReplaceNext(null, null);
					e.Handled = true;
				}
				else if (e.Key == VirtualKey.Escape)
				{
					IsFindPopupOpen = false;
					e.Handled = true;
				}
				else if (e.Key == VirtualKey.Tab)
				{
					Tbx_Search.Focus(FocusState.Keyboard);
					e.Handled = true;
				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}

		private void Tbx_SearchChanged(object sender, TextChangedEventArgs e)
		{
			try
			{
				searchindex = 0;
				string text = Tbx_Search.Text;
				if (text == "")
				{
					SearchMatches.Clear();
					CanvasScrollbarMarkers.Invalidate();
					CanvasSelection.Invalidate();
					return;
				}

				SearchMatches.Clear();
				for (int iLine = 0; iLine < Lines.Count; iLine++)
				{
					if (IsRegex)
					{
						bool isValidRegex = true;
						MatchCollection coll = null;
						try
						{
							coll = Regex.Matches(Lines[iLine].LineText, text, IsMatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
						}
						catch
						{
							isValidRegex = false;
						}
						if (isValidRegex && coll != null)
						{
							foreach (Match m in coll)
								SearchMatches.Add(new() { Match = m.Value, iChar = m.Index, iLine = iLine });
						}
					}
					else
					{
						int nextindex = 0;

						while (nextindex != -1)
						{
							nextindex = Lines[iLine].LineText.IndexOf(text, nextindex, IsMatchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase);
							if (nextindex != -1)
							{
								SearchMatches.Add(new() { Match = text, iChar = nextindex, iLine = iLine });
								nextindex++;
							}
						}
					}
				}
				if (SearchMatches.Count > 0)
				{
					Selection = new Range(new Place(SearchMatches[0].iChar, SearchMatches[0].iLine));
				}
				CanvasScrollbarMarkers.Invalidate();
				CanvasSelection.Invalidate();
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
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
					SelectedLines[iline].LineText = SelectedLines[iline].LineText.Remove(0, 1);
					if (iline == 0)
						start = start - 1;
					if (iline == SelectedLines.Count - 1)
						end = end - 1;
				}
				else if (SelectedLines[iline].LineText.StartsWith(string.Concat(Enumerable.Repeat("\t", SelectedLines[iline].Indents)) + Language.LineComment))
				{
					SelectedLines[iline].LineText = SelectedLines[iline].LineText.Remove(SelectedLines[iline].Indents, 1);
					if (iline == 0)
						start = start - 1;
					if (iline == SelectedLines.Count - 1)
						end = end - 1;
				}
				else
				{
					SelectedLines[iline].LineText = SelectedLines[iline].LineText.Insert(SelectedLines[iline].Indents, Language.LineComment);
					if (iline == 0)
						start = start + 1;
					if (iline == SelectedLines.Count - 1)
						end = end + 1;
				}
			}

			Selection = new(start, end);
			textChanged();
			CanvasText.Invalidate();
			CanvasScrollbarMarkers.Invalidate();
			LinesChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lines)));
		}

		private async void TextAction_Delete(Range selection, bool cut = false)
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
					//Selection = new(start);

					await Task.Run(() =>
					{
						string storetext = "";
						int removedlines = 0;
						for (int iLine = start.iLine; iLine <= end.iLine; iLine++)
						{
							if (end.iLine == start.iLine)
							{
								Lines[iLine].LineText = Lines[iLine].LineText.Remove(start.iChar, end.iChar - start.iChar);
							}
							else if (iLine == start.iLine)
							{
								if (start.iChar < Lines[iLine].Count)
									Lines[iLine].LineText = Lines[iLine].LineText.Remove(start.iChar);
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

						Lines[start.iLine].LineText += storetext;
					});

				}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
			textChanged();
			Invalidate();
		}

		private void TextAction_Find()
		{
			IsFindPopupOpen = true;
			if (!SelectedText.Contains("\n"))
				Tbx_Search.Text = SelectedText;
			Tbx_Search.Focus(FocusState.Keyboard);
			Tbx_Search.SelectionStart = Tbx_Search.Text.Length;
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

		private async void TextAction_Paste(string texttopaste = null, Place placetopaste = null, DragDropModifiers dragDropModifiers = DragDropModifiers.None)
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
				}

				Language lang = Language;
				int i = 0;
				await Task.Run(() =>
				{
					int tabcount = Lines[place.iLine].Indents;
					string stringtomove = "";
					foreach (string line in text.Split('\n', StringSplitOptions.None))
					{
						if (i == 0 && text.Count(x => x == '\n') == 0)
						{
							if (place.iChar < Lines[place.iLine].LineText.Length)
								Lines[place.iLine].LineText = Lines[place.iLine].LineText.Insert(place.iChar, line);
							else
								Lines[place.iLine].LineText += line;
						}
						else if (i == 0)
						{
							stringtomove = Lines[place.iLine].LineText.Substring(place.iChar);
							Lines[place.iLine].LineText = Lines[place.iLine].LineText.Remove(place.iChar) + line;
						}
						else
						{
							Lines.Insert(place.iLine + i, new Line(lang) { LineNumber = place.iLine + 1 + i, LineText = string.Concat(Enumerable.Repeat("\t", tabcount)) + line, IsUnsaved = true });
						}
						i++;
					}
					if (!string.IsNullOrEmpty(stringtomove))
						Lines[place.iLine + i].LineText += stringtomove;
				});

				if (IsSelection && place >= Selection.VisualEnd && dragDropModifiers != DragDropModifiers.Control)
				{
					TextAction_Delete(Selection);
				}

				Place end = new(i == 1 ? place.iChar + text.Length : Lines[place.iLine + i - 1].Count, place.iLine + i - 1);
				Selection = new(end);
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
			textChanged();
			Invalidate();
		}

		private void textChanged()
		{
			try
			{
				RecalcLineNumbers();
				IsSettingValue = true;
				string t = string.Join("\n", Lines.Select(x => x.LineText));
				Text = t;
				if (Lines[CursorPlace.iLine].LineText.Length > maxchars)
				{
					maxchars = Lines[CursorPlace.iLine].LineText.Length;
					HorizontalScroll.Maximum = (maxchars + 2 + Lines[CursorPlace.iLine].Indents * TabLength) * CharWidth - Scroll.ActualWidth + Width_Left;
					VerticalScroll.Visibility = Lines.Count * CharHeight > TextControl.ActualHeight ? Visibility.Visible : Visibility.Collapsed;
					HorizontalScroll.Visibility = maxchars * CharHeight > TextControl.ActualWidth ? Visibility.Visible : Visibility.Collapsed;
				}
				IsSettingValue = false;
				TextChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}
		bool tempFocus = false;
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
					tempFocus = true;

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
					tempFocus = true;
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
				tempFocus = true;

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

					TextAction_Paste(text, await PointToPlace(e.GetPosition(TextControl)), e.Modifiers);
					e.Handled = true;
					if (tempFocus)
					{
						tempFocus = false;
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

		private async void TextControl_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			try
			{
				PointerPoint point = e.GetCurrentPoint(TextControl);
				if (point.Properties.IsLeftButtonPressed && isSelecting)
				{
					if (point.Position.Y < 0)
					{
						DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
						Timer.Tick += async (a, b) =>
						{
							PointerPoint pointup = e.GetCurrentPoint(TextControl);
							if (pointup.Position.Y > 0 | !pointup.Properties.IsLeftButtonPressed)
							{
								((DispatcherTimer)a).Stop();
							}
							VerticalScroll.Value += pointup.Position.Y;
							Selection = new(Selection.Start, await PointToPlace(pointup.Position));
						};
						Timer.Start();
					}
					else if (point.Position.Y >= Scroll.ActualHeight - 2 * CharHeight)
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
						DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
						Timer.Tick += async (a, b) =>
						{
							PointerPoint pointleft = e.GetCurrentPoint(TextControl);
							if (pointleft.Position.X > 0 | !pointleft.Properties.IsLeftButtonPressed)
							{
								((DispatcherTimer)a).Stop();
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
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}

		private void TextControl_PointerLost(object sender, PointerRoutedEventArgs e)
		{
			isSelecting = false;
			isLineSelect = false;
			isDragging = false;
			tempFocus = false;
		}

		PointerPoint CurrentPointerPoint { get; set; }

		private async void TextControl_PointerMoved(object sender, PointerRoutedEventArgs e)
		{
			try
			{
				CurrentPointerPoint = e.GetCurrentPoint(TextControl);

				if (!isDragging)
					if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse | e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Pen)
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
								//Cursor = new CoreCursor(CoreCursorType.Arrow, 1);
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
								//if (place.iChar < Lines[place.iChar].Count)
								//		ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
								//	else
								//		ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);



							}
							else
								ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
							//Cursor = new CoreCursor(CoreCursorType.IBeam, 1);
						}
						e.Handled = true;
					}
			}
			catch (Exception ex)
			{
				ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
			}
		}

		private async void TextControl_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			IsSuggesting = false;
			bool hasfocus = Focus(FocusState.Pointer);
			PointerPoint currentpoint = e.GetCurrentPoint(TextControl);
			try
			{
				if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch)
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
				else if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse | e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Pen)
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

		public void SelectLine(Place start)
		{
			Place end = new(Lines[start.iLine].Count, start.iLine);
			Selection = new(start, end);
		}

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

		private async void TextControl_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			try
			{
				PointerPoint currentpoint = e.GetCurrentPoint(TextControl);
				Place place = await PointToPlace(currentpoint.Position);
				isSelecting = false;
				isLineSelect = false;
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

		private void VerticalScroll_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
			//Cursor = new CoreCursor(CoreCursorType.Arrow, 1);
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

		private void VerticalScroll_Scroll(object sender, ScrollEventArgs e)
		{
		}

		private void Lbx_Suggestions_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
		{
			insertSuggestion();
			textChanged();
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
						insertSuggestion();
						textChanged();
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

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
		}
	}
}