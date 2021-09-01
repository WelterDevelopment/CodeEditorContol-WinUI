using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input.Experimental;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace CodeWriter_WinUI
{
    public partial class CodeWriter : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ShowControlCharactersProperty = DependencyProperty.Register("ShowControlCharacters", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).OnShowControlCharactersChanged(d, e)));

       
        public static new readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSize", typeof(int), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).FontSizeChanged(d, e)));

        public static new readonly DependencyProperty RequestedThemeProperty = DependencyProperty.Register("RequestedTheme", typeof(ElementTheme), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).RequestedThemeChanged(d, e)));

        public static readonly DependencyProperty ScrollPositionProperty = DependencyProperty.Register("ScrollPosition", typeof(Place), typeof(CodeWriter), new PropertyMetadata(new Place(), (d, e) => ((CodeWriter)d).OnScrollPositionChanged(d, e)));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(CodeWriter), new PropertyMetadata("", (d, e) => ((CodeWriter)d).OnTextChanged(d, e)));
        public static readonly DependencyProperty TabLengthProperty = DependencyProperty.Register("TabLength", typeof(int), typeof(CodeWriter), new PropertyMetadata(2, (d, e) => ((CodeWriter)d).OnTabLengthChanged(d, e)));
        public static readonly DependencyProperty ShowLineNumbersProperty = DependencyProperty.Register("ShowLineNumbers", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
        public static readonly DependencyProperty ShowScrollbarMarkersProperty = DependencyProperty.Register("ShowScrollbarMarkers", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
        public static readonly DependencyProperty ShowLineMarkersProperty = DependencyProperty.Register("ShowLineMarkers", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));
        public static readonly DependencyProperty IsFoldingEnabledProperty = DependencyProperty.Register("IsFoldingEnabled", typeof(bool), typeof(CodeWriter), new PropertyMetadata(true, (d, e) => ((CodeWriter)d).Invalidate()));

        private void OnTabLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Invalidate();
        }
        private void OnShowControlCharactersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CanvasText.Invalidate();
        }


        public void Action_Add(MenuFlyoutItemBase item)
        {
            ContextMenu.Items.Add(item);
        }

        public void Action_Remove(MenuFlyoutItemBase item)
        {
            ContextMenu.Items.Remove(item);
        }

        public Dictionary<Token, Color> Tokens = new()
        {
            { Token.Normal, Color.FromArgb(255, 220, 220, 220) },
            { Token.Command, Color.FromArgb(255, 50, 130, 210) },
            { Token.Environment, Color.FromArgb(255, 50, 190, 150) },
            { Token.Comment, Color.FromArgb(255, 40, 190, 90) },
            { Token.Key, Color.FromArgb(255, 150, 120, 200) },
            { Token.Bracket, Color.FromArgb(255, 80, 40, 180) },
            { Token.Reference, Color.FromArgb(255, 180, 120, 40) },
            { Token.Math, Color.FromArgb(255, 220, 160, 60) },
            { Token.Symbol, Color.FromArgb(255, 140, 200, 240) },
            { Token.Style, Color.FromArgb(255, 200, 140, 100) },
            { Token.Array, Color.FromArgb(255, 200, 100, 80) },
        };

        private int iCharOffset = 0;

        private bool invoked = false;

        private bool isCanvasLoaded = false;

        private bool IsSettingValue = false;

        private int maxchars = 0;

        private int MaxFontSize = 64;

        private int MinFontSize = 6;

        private List<VirtualKey> Modifiers = new List<VirtualKey>();

        private Point pos = new Point() { };

        private int startFontsize = 16;

        private List<EditAction> EditActionHistory = new();
        private List<Place> CursorPlaceHistory = new();

        public CodeWriter()
        {
            InitializeComponent();
            Command_Copy = new RelayCommand(() => TextAction_Copy());
            Command_Paste = new RelayCommand(() => { TextAction_Paste(); });
            Command_Delete = new RelayCommand(() => TextAction_Delete());
            Command_Cut = new RelayCommand(() => TextAction_Delete(true));
            Command_SelectAll = new RelayCommand(() => TextAction_SelectText());
            Command_Find = new RelayCommand(() => TextAction_Find());
            Invalidate();
        }

        private void TextAction_Find()
        {
            IsFindPopupOpen = true;
            Tbx_Search.Focus( FocusState.Keyboard);
        }

        public void TextAction_SelectText(SelectionRange range = null)
        {
            if (range == null && Lines.Count>0)
            {
                Selection = new SelectionRange(new(0, 0), new(Lines.Last().Count, Lines.Count-1));
            }
        }

        public event ErrorEventHandler ErrorOccured;

        public event PropertyChangedEventHandler TextChanged;

        public enum ScrollOrientation
        {
            VerticalScroll,
            HorizontalScroll
        }

        public new SolidColorBrush Background { get => Get(new SolidColorBrush(Color.FromArgb(255, 10, 10, 10))); set => Set(value); }

        public int CharHeight { get => Get(16); set { Set(value); } }

        public int CharWidth { get => Get(8); set { Set(value); } }

        public bool IsFindPopupOpen { get => Get(false); set { Set(value); if (!value) { Tbx_Search.Text = ""; } } }
        public bool IsMatchCase { get => Get(false); set { Set(value); Tbx_SearchChanged(null,null); } }
        public bool IsRegex { get => Get(false); set { Set(value); Tbx_SearchChanged(null, null); } }

        public Color Color_WeakMarker { get => Get(Color.FromArgb(255, 40, 40, 40)); set => Set(value); }
        public Color Color_FoldingMarker { get => Get(Color.FromArgb(255, 140, 140, 140)); set => Set(value); }
        public Color Color_LeftBackground { get => Get(Color.FromArgb(255, 40, 40, 40)); set => Set(value); }
        public Color Color_LineNumber { get => Get(Color.FromArgb(255, 40, 140, 160)); set => Set(value); }
        public Color Color_Selection { get => Get(Color.FromArgb(255, 25, 50, 80)); set => Set(value); }
        public Color Color_Beam { get => Get(Color.FromArgb(255, 200, 200, 200)); set => Set(value); }

        public ICommand Command_Copy { get; set; }

        public ICommand Command_Cut { get; set; }

        public ICommand Command_Delete { get; set; }

        public ICommand Command_SelectAll { get; set; }

        public ICommand Command_Find { get; set; }

        public ICommand Command_Paste { get; set; }

        public CoreCursor Cursor
        {
            get { return base.ProtectedCursor; }
            set { base.ProtectedCursor = value; }
        }

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

                    if ((value.iLine+1) * CharHeight <= VerticalScroll.Value)
                        VerticalScroll.Value = value.iLine * CharHeight;
                    else if ((value.iLine+2) * CharHeight  > VerticalScroll.Value + Scroll.ActualHeight)
                        VerticalScroll.Value = Math.Min((value.iLine+2) * CharHeight - Scroll.ActualHeight, VerticalScroll.Maximum);
                }

                int x = CharWidth * (value.iChar - iCharOffset) + Width_Left;
                int y = CharHeight * (value.iLine - VisibleLines[0].LineNumber + 1);
                CursorPoint = new Point(x, y + CharHeight);

                CanvasBeam.Invalidate();
                CanvasScrollbarMarkers.Invalidate();
            }
        }

        public Point CursorPoint { get => Get(new Point()); set => Set(value); }

        public new int FontSize
        {
            get => (int)GetValue(FontSizeProperty);
            set { SetValue(FontSizeProperty, value); }
        }

        public bool ShowLineNumbers
        {
            get => (bool)GetValue(ShowLineNumbersProperty);
            set { SetValue(ShowLineNumbersProperty, value); }
        }

        public bool ShowScrollbarMarkers
        {
            get => (bool)GetValue(ShowScrollbarMarkersProperty);
            set { SetValue(ShowScrollbarMarkersProperty, value); }
        }

        public bool ShowLineMarkers
        {
            get => (bool)GetValue(ShowLineMarkersProperty);
            set { SetValue(ShowLineMarkersProperty, value); }
        }

        public bool IsFoldingEnabled
        {
            get => (bool)GetValue(IsFoldingEnabledProperty);
            set { SetValue(IsFoldingEnabledProperty, value); }
        }

        public int HorizontalOffset { get => Get(0); set { Set(value); } }

        public bool IsSelection { get => Get(false); set => Set(value); }

        private Place SuggestionStart = new Place();

        public bool IsSuggesting
        {
            get => Get(false);
            set
            {
                Set(value);
                if (value)
                {
                    SuggestionIndex = 0;
                }
            }
        }

        public List<Line> Lines { get => Get(new List<Line>()); set => Set(value); }

        public int TabLength
        {
            get => (int)GetValue(TabLengthProperty);
            set => SetValue(TabLengthProperty, value);
        }

        public bool ShowControlCharacters
        {
            get => (bool)GetValue(ShowControlCharactersProperty);
            set => SetValue(ShowControlCharactersProperty, value);
        }

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

        public string SelectedText
        {
            get
            {
                string text = "";
                if (Selection.Start == Selection.End)
                    return "";

                Place start;
                Place end;

                if (Selection.Start < Selection.End)
                {
                    start = new(Selection.Start);
                    end = new(Selection.End);
                }
                else
                {
                    start = new(Selection.End);
                    end = new(Selection.Start);
                }

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

        public SelectionRange Selection
        {
            get => Get(new SelectionRange());
            set
            {
                Set(value);
                CursorPlace = new Place(value.End.iChar, value.End.iLine);
                IsSelection = Selection.Start.iChar != Selection.End.iChar | Selection.Start.iLine != Selection.End.iLine;
                CanvasSelection.Invalidate();
            }
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public List<Line> VisibleLines { get; set; } = new List<Line>();
        public int Width_Left { get => (int)Width_LineNumber + (int)Width_ErrorMarker + (int)Width_WarningMarker + (int)Width_FoldingMarker + Width_TextIndent; }
        public int Width_LineNumber { get => Get(12); set => Set(value); }
        public bool WordWrap { get; private set; } = true;

        private List<IntelliSense> Commands
        {
            get => Get(new List<IntelliSense>() {
            new IntelliSense(){ Text = @"\foo", IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
            new IntelliSense(){ Text = @"\bar", IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
            new IntelliSense(){ Text = @"\foobar", IntelliSenseType = IntelliSenseType.Command, Token = Token.Command, Description = ""},
        }); set => Set(value);
        }

        private List<IntelliSense> Options
        {
            get => Get(new List<IntelliSense>() {
            new IntelliSense(){ Text = @"foofoo", IntelliSenseType = IntelliSenseType.Argument, Token = Token.Dimension, Description = ""},
            new IntelliSense(){ Text = @"barbar", IntelliSenseType = IntelliSenseType.Argument, Token = Token.Reference, Description = ""},
            new IntelliSense(){ Text = @"foofoobarbar", IntelliSenseType = IntelliSenseType.Argument, Token = Token.Text, Description = ""},
        }); set => Set(value);
        }

        private bool isLineSelect { get; set; } = false;
        private bool isSelecting { get; set; } = false;
        private int SuggestionIndex { get => Get(0); set => Set(value); }
        private List<IntelliSense> Suggestions { get => Get(Commands); set => Set(value); }
        private int Width_ErrorMarker { get; set; } = 12;
        private int Width_FoldingMarker { get; set; } = 24;
        private int Width_TextIndent { get => CharWidth/2; }
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

        public void Invalidate()
        {
            try
            {
                DrawText();
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

        private void CanvasBeam_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            try
            {
                if (VisibleLines.Count > 0)
                {
                    int x = (int)(Width_Left + HorizontalOffset + CursorPlace.iChar * CharWidth);
                    int y = (int)((CursorPlace.iLine - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);

                    for (int i = 0; i< CursorPlace.iChar; i++)
                    {
                        if (Lines[CursorPlace.iLine][i].C == '\t')
                        {
                            x += CharWidth * (TabLength -1);
                        }
                    }

                    if (y <= TextControl.ActualHeight && y >= 0 && x <= TextControl.ActualWidth && x >= Width_Left)
                        args.DrawingSession.DrawLine(new Vector2(x, y), new Vector2(x, y + CharHeight), ActualTheme == ElementTheme.Light ? Color_Beam.InvertColorBrightness() : Color_Beam, 2f);

                    int xms = (int)(Width_Left);
                    int iCharStart = iCharOffset;
                    int xme = (int)TextControl.ActualWidth;
                    int iCharEnd = iCharStart + (int)((xme - xms) / CharWidth);

                    for (int iChar = iCharStart; iChar < iCharEnd; iChar++)
                    {
                        int xs = (int)((iChar - iCharStart) * CharWidth) + xms;
                        if (iChar % 10 == 0)
                            args.DrawingSession.DrawLine(xs, 0, xs, CharHeight / 8, new CanvasSolidColorBrush(sender, Color_LineNumber), 2f);
                    }

                    if (Selection.Start == CursorPlace)
                    {
                        args.DrawingSession.DrawRoundedRectangle(Width_Left, y, (int)TextControl.ActualWidth - Width_Left, CharHeight, 2, 2, ActualTheme == ElementTheme.Light ? Color_FoldingMarker.InvertColorBrightness() : Color_FoldingMarker, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        private void DrawSelection(CanvasDrawingSession session, int Line, int StartChar, int EndChar,  SelectionType selectionType = SelectionType.Selection )
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

        private void CanvasSelection_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            try
            {
                if (VisibleLines.Count > 0)
                {
                    if (IsSelection)
                    {
                        Place start = new Place();
                        Place end = new Place();

                        if (Selection.Start < Selection.End)
                        {
                            start = new(Selection.Start);
                            end = new(Selection.End);
                        }
                        else
                        {
                            start = new(Selection.End);
                            end = new(Selection.Start);
                        }

                        if (start.iLine < VisibleLines[0].LineNumber - 1)
                        {
                            start.iLine = VisibleLines[0].LineNumber - 1;
                            start.iChar = 0;
                        }

                        if (end.iLine > VisibleLines.Last().LineNumber - 1)
                        {
                            end.iLine = VisibleLines.Last().LineNumber - 1;
                            end.iChar = VisibleLines.Last().Count;
                        }

                        for (int lp = start.iLine; lp <= end.iLine; lp++)
                            if (lp >= VisibleLines[0].LineNumber - 1 && lp <= VisibleLines.Last().LineNumber - 1)
                                if (start.iLine == end.iLine)
                                    DrawSelection(args.DrawingSession, start.iLine, start.iChar, end.iChar);
                                else if (lp == start.iLine)
                                    DrawSelection(args.DrawingSession, lp, start.iChar, Lines[lp].Count + 1);
                                else if (lp > start.iLine && lp < end.iLine)
                                    DrawSelection(args.DrawingSession, lp, 0, Lines[lp].Count + 1);
                                else if (lp == end.iLine)
                                    DrawSelection(args.DrawingSession, lp, 0, end.iChar);
                    }
                    
                    foreach(SearchMatch match in SearchMatches)
                    {
                        if (match.iLine >= VisibleLines[0].LineNumber - 1 && match.iLine <= VisibleLines.Last().LineNumber - 1)
                            DrawSelection(args.DrawingSession, match.iLine, match.iChar, match.iChar + match.Match.Length, SelectionType.SearchMatch);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

       
        private void CanvasText_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            try
            {
                CanvasTextFormat textFormat = new CanvasTextFormat
                {
                    FontFamily = "Consolas",
                    FontSize = FontSize
                };

                Size size = MeasureTextSize(sender.Device, "│", textFormat);
                Size sizew = MeasureTextSize(sender.Device, "–", textFormat);

                CharHeight = (int)(size.Height * 1.03f);
                CharWidth = (int)(sizew.Width * 1.31f);
                isCanvasLoaded = true;

                Invalidate();
                Selection = new(new(0, 0));
            }
            catch (Exception ex)
            {
                ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        public void Save()
        {
            Lines.ForEach(x => x.IsUnsaved = false);
            CanvasText.Invalidate();
        }

        private void CanvasText_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            try
            {
                if (VisibleLines.Count > 0)
                {
                    int foldPos = Width_LineNumber + Width_ErrorMarker + Width_WarningMarker;
                    int errorPos = Width_LineNumber;
                    int warningPos = Width_LineNumber + Width_ErrorMarker;
                    for (int iLine = VisibleLines[0].LineNumber - 1; iLine < VisibleLines.Last().LineNumber; iLine++)
                    {
                        float y = CharHeight * (iLine - VisibleLines[0].LineNumber + 1);
                        float x = 0;
                        args.DrawingSession.FillRectangle(0, y, Width_Left - Width_TextIndent, CharHeight, Color_LeftBackground);

                        if (ShowLineNumbers)
                            args.DrawingSession.DrawText((iLine + 1).ToString(), CharWidth * IntLength(Lines.Count), y, Color_LineNumber, new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize, HorizontalAlignment = CanvasHorizontalAlignment.Right });

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
                                args.DrawingSession.FillRectangle(warningPos, y, Width_ErrorMarker, CharHeight, Color.FromArgb(255, 120, 180, 230));

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

                        int lastChar = Math.Min(iCharOffset + (int)(Scroll.ActualWidth / CharWidth), Lines[iLine].Count);
                        int indents = 0;


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
                                x = Width_Left + CharWidth * (iChar + indents * (TabLength - 1) - iCharOffset);
                                args.DrawingSession.DrawText(c.C.ToString(), x, y, ActualTheme == ElementTheme.Light ? Tokens[c.T].InvertColorBrightness() : Tokens[c.T], new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize });
                            }
                        }
                        if (ShowControlCharacters && iLine < Lines.Count - 1 && lastChar >= iCharOffset - indents * (TabLength - 1))
                        {
                            x = Width_Left + CharWidth * (lastChar + indents * (TabLength - 1) - iCharOffset);
                            CanvasPathBuilder enterpath = new CanvasPathBuilder(sender);

                            enterpath.BeginFigure(CharWidth * 0.2f, CharHeight * 2 / 4);
                            enterpath.AddLine(CharWidth * 0.5f, CharHeight * 2 / 4);
                            enterpath.AddLine(CharWidth * 0.5f, CharHeight * 4 / 4);
                            enterpath.EndFigure(CanvasFigureLoop.Open);

                            enterpath.BeginFigure(CharWidth * 0.2f, CharHeight * 3 / 4);
                            enterpath.AddLine(CharWidth * 0.5f, CharHeight * 4 / 4);
                            enterpath.AddLine(CharWidth * 0.8f, CharHeight * 3 / 4);
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
                        if (Selection.Start.iChar < Selection.End.iChar)
                        {
                            Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(Selection.Start.iChar, Selection.End.iChar - Selection.Start.iChar);
                            Selection = new SelectionRange(Selection.Start, Selection.Start);
                        }
                        else
                        {
                            Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(Selection.End.iChar, Selection.Start.iChar - Selection.End.iChar);
                            Selection = new SelectionRange(Selection.End, Selection.End);
                        }
                    }

                    if (args.Character == ',' && !IsSuggesting && IsInsideBrackets(CursorPlace))
                    {
                        SuggestionStart = CursorPlace;
                        Suggestions = Options;
                        IsSuggesting = true;
                    }

                    if (args.Character == '\\')
                    {
                        SuggestionStart = CursorPlace;
                        Suggestions = Commands;
                        IsSuggesting = true;
                    }

                    if (args.Character == '[')
                    {
                        SuggestionStart = CursorPlace;
                        Suggestions = Options;
                        IsSuggesting = true;
                    }

                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, args.Character.ToString());
                    if (args.Character == '[')
                    {
                        Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar + 1, "]");
                    }
                    CursorPlace = new(CursorPlace.iChar + 1, CursorPlace.iLine);
                    textChanged();
                    CanvasText.Invalidate();
                }
            }
            catch (Exception ex)
            {
                ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
            }
            args.Handled = true;
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
            Debug.WriteLine(string.Join("\n", pairs.Select(x => x.iOpen.ToString())));

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

        private void DrawSelectionCell(CanvasDrawingSession session, int x, int y, SelectionType selectionType, int w = 1)
        {
            Color color = Color_Selection;
            if (selectionType == SelectionType.SearchMatch)
            {
                color = Color.FromArgb(255,60,60,60);
            }
            else if (selectionType == SelectionType.WordLight)
            {
                color = Colors.DeepSkyBlue;
            }
            session.FillRectangle(x, y, CharWidth*w+1, CharHeight+1, ActualTheme == ElementTheme.Light ? color.InvertColorBrightness() : color);
        }

        public int CurrentLine = 0;

        private void DrawText()
        {
            if (isCanvasLoaded)
            {
                CanvasTextFormat textFormat = new CanvasTextFormat
                {
                    FontFamily = "Consolas",
                    FontSize = FontSize
                };

                Size size = MeasureTextSize(CanvasText.Device, "│", textFormat);
                Size sizew = MeasureTextSize(CanvasText.Device, "–", textFormat);

                CharHeight = (int)(size.Height * 1.03f);
                CharWidth = (int)(sizew.Width * 1.31f);
            }

            if (VerticalScroll != null && HorizontalScroll != null && Lines != null)
            {
                

                VerticalScroll.Maximum = (Lines.Count + 2) * CharHeight - Scroll.ActualHeight;
                VerticalScroll.SmallChange = CharHeight;
                VerticalScroll.LargeChange = 3 * CharHeight;

                maxchars = 0;
                foreach (Line l in Lines)
                {
                    maxchars = l.Count > maxchars ? l.Count : maxchars;
                }

                HorizontalScroll.Maximum = (maxchars + 1) * CharWidth - Scroll.ActualWidth + Width_Left;
                HorizontalScroll.SmallChange = CharWidth;
                HorizontalScroll.LargeChange = 3 * CharWidth;

                VerticalScroll.Visibility = Lines.Count * CharHeight > TextControl.ActualHeight ? Visibility.Visible : Visibility.Collapsed;
                HorizontalScroll.Visibility = maxchars * CharHeight > TextControl.ActualWidth ? Visibility.Visible : Visibility.Collapsed;

                int StartLine = (int)(VerticalScroll.Value / CharHeight) + 1;                
                int EndLine = Math.Min((int)((VerticalScroll.Value + Scroll.ActualHeight) / CharHeight) + 1, Lines.Count);
                VisibleLines.Clear();
                foreach (Line l in Lines)
                {
                    if (l.LineNumber <= EndLine && l.LineNumber >= StartLine)
                    {
                        VisibleLines.Add(l);
                    }
                }


                Width_LineNumber = ShowLineNumbers ? CharWidth * IntLength(Lines.Count) : 0;
                Width_FoldingMarker = IsFoldingEnabled ? CharWidth : 0;
                Width_ErrorMarker = ShowLineNumbers ? CharWidth/2 : 0;
                Width_WarningMarker = ShowLineNumbers ? CharWidth / 2 : 0;



                CanvasBeam.Invalidate();
                CanvasSelection.Invalidate();
                CanvasText.Invalidate();
                CanvasScrollbarMarkers.Invalidate();
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

        private void InitializeLines(string text)
        {
            Lines.Clear();
            string[] lines = text.Contains("\r\n") ? text.Split("\r\n", StringSplitOptions.None) : text.Split("\n", StringSplitOptions.None);
            int lineNumber = 1;
            foreach (string line in lines)
            {
                Line l = new Line() { LineNumber = lineNumber, LineText = line };
                Lines.Add(l);
                lineNumber++;
            }
            if (isCanvasLoaded)
            {
                
                Invalidate();
                Selection = new(new(0, 0));
            }
        }

        private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            Modifiers.Clear();
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

        private Size MeasureTextSize(CanvasDevice device, string text, CanvasTextFormat textFormat, float limitedToWidth = 0.0f, float limitedToHeight = 0.0f)
        {
            CanvasTextLayout layout = new(device, text, textFormat, limitedToWidth, limitedToHeight);

            double width = layout.DrawBounds.Width;
            double height = layout.DrawBounds.Height;

            return new(width, height);
        }

        private void FontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        {
            int currline = 0;
            if (VerticalScroll != null)
            {
                currline = (int)VerticalScroll.Value / CharHeight;
            }
            Invalidate(); 
            if (VerticalScroll != null)
            {
                VerticalScroll.Value = currline * CharHeight;
            }
        }

        private ElementTheme ActualTheme = ElementTheme.Dark;

        private void RequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
            }

            else
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30));
                Color_LeftBackground = Color.FromArgb(255, 25, 25, 25);
            }

            Invalidate();
        }

        private void OnScrollPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        private void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d as CodeWriter).IsSettingValue)
            {
                InitializeLines((string)e.NewValue);
            }
            TextChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }

        private Place PointerToPlace(Point currentpoint)
        {
            int iline = Math.Min((int)(currentpoint.Y / CharHeight) + VisibleLines[0].LineNumber - 1, Lines.Count - 1);
            int ichar = 0;
            if ((int)currentpoint.X - Width_Left - HorizontalOffset > 0)
            {
                int visualcharplace = (int)((currentpoint.X - Width_Left - HorizontalOffset + CharWidth * 2 / 3) / CharWidth);
                int currentvisibleplace = 0;
                int actualcharplace = visualcharplace;
                for (int i = 0; i < Math.Min(Lines[iline].Count, visualcharplace); i++)
                {
                    if (currentvisibleplace < visualcharplace)
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

                ichar = Math.Min(actualcharplace, Lines[iline].Count);
            }

            return new Place(ichar, iline);
        }

        private void Scroll_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                if (!isSelecting)
                {
                    if (e.Key == VirtualKey.Control)
                    {
                        Modifiers.Add(VirtualKey.Control);
                    }
                    string storetext = "";
                    switch (e.Key)
                    {
                        case VirtualKey.Escape:
                            IsSuggesting = false;
                            break;

                        case VirtualKey.Tab:
                            Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, "\t");
                            CursorPlace = new(CursorPlace.iChar + 1, CursorPlace.iLine);
                            textChanged();
                            CanvasText.Invalidate();
                            e.Handled = true;
                            break;

                        case VirtualKey.Enter:
                            if (IsSuggesting)
                            {
                                if (Suggestions[SuggestionIndex].IntelliSenseType == IntelliSenseType.Command)
                                {
                                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(SuggestionStart.iChar, CursorPlace.iChar - SuggestionStart.iChar);
                                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(SuggestionStart.iChar, Suggestions[SuggestionIndex].Text);
                                    Selection = new(new(SuggestionStart.iChar + Suggestions[SuggestionIndex].Text.Length, CursorPlace.iLine));
                                }
                                else
                                {
                                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(SuggestionStart.iChar + 1, CursorPlace.iChar - (SuggestionStart.iChar + 1));
                                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(SuggestionStart.iChar + 1, Suggestions[SuggestionIndex].Text);
                                    Selection = new(new(SuggestionStart.iChar + 1 + Suggestions[SuggestionIndex].Text.Length, CursorPlace.iLine));
                                }
                                IsSuggesting = false;

                                textChanged();
                                CanvasText.Invalidate();
                                break;
                            }
                            if (IsSelection)
                            {
                                TextAction_Delete();
                            }
                            if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count - 1)
                            {
                                storetext = Lines[CursorPlace.iLine].LineText.Substring(CursorPlace.iChar);
                                Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar);
                            }
                            Lines.Insert(CursorPlace.iLine + 1, new Line() { LineNumber = CursorPlace.iLine, LineText = storetext });
                            for (int i = CursorPlace.iLine + 1; i < Lines.Count; i++)
                                Lines[i].LineNumber = i + 1;
                            Place newselect = CursorPlace;
                            newselect.iLine++;
                            newselect.iChar = 0;
                            Selection = new SelectionRange(newselect, newselect);
                            textChanged();
                            Invalidate();
                            e.Handled = true;
                            break;

                        case VirtualKey.Delete:
                            if (!IsSelection)
                            {
                                if (CursorPlace.iChar == Lines[CursorPlace.iLine].Count && CursorPlace.iLine < Lines.Count - 1)
                                {
                                    storetext = Lines[CursorPlace.iLine + 1].LineText;
                                    Lines.RemoveAt(CursorPlace.iLine + 1);
                                    Lines[CursorPlace.iLine].LineText += storetext;
                                    textChanged();
                                    Invalidate();
                                }
                                else if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
                                {
                                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar, 1);
                                    textChanged();
                                    CanvasText.Invalidate();
                                }
                            }
                            else
                            {
                                TextAction_Delete();
                            }
                            break;

                        case VirtualKey.Back:
                            if (!IsSelection)
                            {
                                if (CursorPlace.iChar == 0 && CursorPlace.iLine > 0)
                                {
                                    storetext = Lines[CursorPlace.iLine].LineText;
                                    Lines.RemoveAt(CursorPlace.iLine);
                                    Place newplace = new(Lines[CursorPlace.iLine - 1].Count, CursorPlace.iLine - 1);
                                    Lines[newplace.iLine].LineText += storetext;
                                    Selection = new SelectionRange(newplace);
                                    
                                }
                                else
                                {
                                    if (Lines[CursorPlace.iLine].LineText[CursorPlace.iChar - 1] == '\\' | Lines[CursorPlace.iLine].LineText[CursorPlace.iChar - 1] == '[')
                                    {
                                        IsSuggesting = false;
                                    }
                                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar - 1, 1);
                                    Place newplace = CursorPlace;
                                    newplace.iChar--;
                                    Selection = new SelectionRange(newplace);
                                }
                                textChanged();
                                Invalidate();
                            }
                            else
                            {
                                TextAction_Delete();
                            }
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
                                Place newplace = CursorPlace;
                                newplace.iLine--;
                                newplace.iChar = Math.Min(Lines[newplace.iLine].Count, newplace.iChar);
                                Selection = new(newplace, newplace);
                            }
                            break;

                        case VirtualKey.Down:
                            if (IsSuggesting)
                            {
                                if (SuggestionIndex < Suggestions.Count - 1)
                                    SuggestionIndex++;
                                else
                                    SuggestionIndex = 0;
                                break;
                            }
                            if (CursorPlace.iLine < Lines.Count - 1)
                            {
                                Place newplace = CursorPlace;
                                newplace.iLine++;
                                newplace.iChar = Math.Min(Lines[newplace.iLine].Count, newplace.iChar);
                                Selection = new(newplace, newplace);
                            }
                            break;

                        case VirtualKey.Left:
                            IsSuggesting = false;
                            if (CursorPlace.iChar > 0)
                            {
                                Place newplace = CursorPlace;
                                newplace.iChar--;
                                Selection = new(newplace);
                            }
                            else if (CursorPlace.iLine > 0)
                            {
                                Place newplace = CursorPlace;
                                newplace.iLine--;
                                newplace.iChar = Lines[CursorPlace.iLine].Count;
                                Selection = new(newplace);
                            }
                            else if (IsSelection)
                                Selection = new(Selection.VisualStart);
                            break;

                        case VirtualKey.Right:
                            IsSuggesting = false;
                            if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
                            {
                                Place newplace = new(CursorPlace.iChar, CursorPlace.iLine);
                                newplace.iChar++;
                                Selection = new(newplace);
                            }
                            else if (CursorPlace.iLine < Lines.Count - 1)
                            {
                                Place newplace = CursorPlace;
                                newplace.iLine++;
                                newplace.iChar = 0;
                                Selection = new(newplace);
                            }
                            else if (IsSelection)
                                Selection = new(Selection.VisualEnd);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        private void Scroll_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control)
            {
                Modifiers.Clear();
            }
        }

        private void Scroll_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint bla = e.GetCurrentPoint(Scroll);
            int mwd = bla.Properties.MouseWheelDelta;
            if (!Modifiers.Contains(VirtualKey.Control))
            {
                mwd = Math.Sign(mwd) * (int)CharHeight;
                if (!bla.Properties.IsHorizontalMouseWheel)
                {
                    VerticalScroll.Value -= mwd * 3;
                }
                else
                {
                    HorizontalScroll.Value = Math.Max(HorizontalScroll.Value + mwd * 3, 0);
                }
            }
            else if (Modifiers.Contains(VirtualKey.Control))
            {
                //FontSize += Math.Sign(mwd);
                int newfontsize = FontSize + Math.Sign(mwd);
                if (newfontsize >= MinFontSize && newfontsize <= MaxFontSize)
                    SetValue(FontSizeProperty, newfontsize);
            }
            e.Handled = true;
        }

        private void Scroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isCanvasLoaded)
                Invalidate();
        }

        private void ScrollContent_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Touch)
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

        private void TextAction_Delete(bool cut = false)
        {
            if (IsSelection)
            {
                if (cut)
                {
                    TextAction_Copy();
                }

                Place start;
                Place end;

                if (Selection.Start < Selection.End)
                {
                    start = new(Selection.Start);
                    end = new(Selection.End);
                }
                else
                {
                    start = new(Selection.End);
                    end = new(Selection.Start);
                }

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

                Selection = new(start);

                textChanged();
                Invalidate();
            }
        }

        private async void TextAction_Paste()
        {
            if (IsSelection)
            {
                TextAction_Delete();
            }
            string text = "";
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                text += await dataPackageView.GetTextAsync();
            }
            int i = 0;
            foreach (string line in text.Split('\n', StringSplitOptions.None))
            {
                if (i == 0)
                {
                    Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, line);
                }
                else
                {
                    Lines.Insert(CursorPlace.iLine + i, new Line() { LineNumber = CursorPlace.iLine +1+i,LineText = line });
                }
                i++;
            }
            
            Place end = new(i == 1 ? CursorPlace.iChar + text.Length : Lines[CursorPlace.iLine+i-1].Count, CursorPlace.iLine + i-1);
            Selection = new(end);
            textChanged();
            Invalidate();
        }

        private void RecalcLineNumbers()
        {
            for (int i = CursorPlace.iLine; i < Lines.Count; i++)
                Lines[i].LineNumber = i + 1;
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
                    HorizontalScroll.Maximum = (maxchars + 3) * CharWidth - Scroll.ActualWidth + Width_Left;
                    VerticalScroll.Visibility = Lines.Count * CharHeight > TextControl.ActualHeight ? Visibility.Visible : Visibility.Collapsed;
                    HorizontalScroll.Visibility = maxchars * CharHeight > TextControl.ActualWidth ? Visibility.Visible : Visibility.Collapsed;
                }
                IsSettingValue = false;
            }
            catch (Exception ex)
            {
                ErrorOccured?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        private void TextControl_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse | e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
            {
                if (isSelecting)
                {
                    if (!isLineSelect)
                    {
                        Selection = new SelectionRange(Selection.Start, PointerToPlace(currentpoint.Position));
                    }
                    else
                    {
                        Place end = PointerToPlace(currentpoint.Position);
                        end.iChar = Lines[end.iLine].Count;
                        Selection = new SelectionRange(Selection.Start, end);
                    }
                }
                else if (isMiddleClickScrolling)
                {
                    middleClickScrollingEndPoint = currentpoint.Position;
                }
                else
                {
                    if (currentpoint.Position.X < Width_Left)
                    {
                        Cursor = new CoreCursor(CoreCursorType.Arrow, 1);
                    }
                    else if (IsSelection)
                    {
                        Place rightpress = PointerToPlace(currentpoint.Position);

                        Place start;
                        Place end;
                        if (Selection.Start < Selection.End)
                        {
                            start = new(Selection.Start);
                            end = new(Selection.End);
                        }
                        else
                        {
                            start = new(Selection.End);
                            end = new(Selection.Start);
                        }
                        if (IsSelection)
                        {
                            if (rightpress <= start || rightpress >= end)
                            {
                                Cursor = new CoreCursor(CoreCursorType.IBeam, 1);
                            }
                            else
                                Cursor = new CoreCursor(CoreCursorType.Arrow, 1);
                        }
                    }
                    else
                        Cursor = new CoreCursor(CoreCursorType.IBeam, 1);
                }
            }
        }
        private bool isMiddleClickScrolling { get => Get(false); set { Set(value);} }
        private Point middleClickScrollingStartPoint = new Point();
        private Point middleClickScrollingEndPoint = new Point();

        private void TextControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            IsSuggesting = false;
            bool hasfocus =  TextControl.Focus( FocusState.Keyboard);
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);
            try
            {
                if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
                {
                }
                else if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse | e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
                {
                    if (currentpoint.Properties.IsLeftButtonPressed)
                    {
                        isLineSelect = currentpoint.Position.X < Width_Left;

                        isSelecting = true;
                        if (!isLineSelect)
                        {
                            if (pos != currentpoint.Position)
                            {
                                Place start = PointerToPlace(currentpoint.Position);
                                Place end = PointerToPlace(currentpoint.Position);
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
                                Place start = PointerToPlace(pos);
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
                            pos = currentpoint.Position;
                        }
                        else
                        {
                            Place start = PointerToPlace(currentpoint.Position);
                            Place end = new(Lines[start.iLine].Count, start.iLine);
                            Selection = new(start, end);
                        }
                        isMiddleClickScrolling = false;
                    }

                    else if (currentpoint.Properties.IsRightButtonPressed)
                    {
                        Place rightpress = PointerToPlace(currentpoint.Position);

                        Place start;
                        Place end;
                        if (Selection.Start < Selection.End)
                        {
                            start = new(Selection.Start);
                            end = new(Selection.End);
                        }
                        else
                        {
                            start = new(Selection.End);
                            end = new(Selection.Start);
                        }
                        if (IsSelection)
                        {
                            if (rightpress <= start || rightpress >= end)
                            {
                                Selection = new SelectionRange(rightpress);
                            }
                        }
                        else
                            Selection = new SelectionRange(rightpress);
                        isMiddleClickScrolling = false;
                    }

                    else if (currentpoint.Properties.IsXButton1Pressed)
                    {
                        if (CursorPlaceHistory.Count > 1)
                        {
                            Selection = new SelectionRange(CursorPlaceHistory[CursorPlaceHistory.Count - 2]);
                            CursorPlaceHistory.Remove(CursorPlaceHistory.Last());
                        }
                    }

                    else if (currentpoint.Properties.IsMiddleButtonPressed)
                    {
                        if (!isMiddleClickScrolling)
                        {
                            if (VerticalScroll.Maximum > Scroll.ActualHeight && HorizontalScroll.Maximum > Scroll.ActualHeight)
                            {
                                isMiddleClickScrolling = true;
                                Cursor = new CoreCursor(CoreCursorType.SizeAll, 1);
                                middleClickScrollingStartPoint = currentpoint.Position;
                                DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
                                Timer.Tick += (a, b) =>
                                {
                                    if (!isMiddleClickScrolling)
                                    {
                                        ((DispatcherTimer)a).Stop();
                                    }
                                    VerticalScroll.Value += middleClickScrollingEndPoint.Y - middleClickScrollingStartPoint.Y;
                                    HorizontalScroll.Value += middleClickScrollingEndPoint.X - middleClickScrollingStartPoint.X;
                                };
                                Timer.Start();
                            }
                            else if (VerticalScroll.Maximum > Scroll.ActualHeight)
                            {
                                isMiddleClickScrolling = true;
                                Cursor = new CoreCursor(CoreCursorType.SizeNorthSouth, 1);
                                middleClickScrollingStartPoint = currentpoint.Position;
                                DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
                                Timer.Tick += (a, b) =>
                                {
                                    if (!isMiddleClickScrolling)
                                    {
                                        ((DispatcherTimer)a).Stop();
                                    }
                                    VerticalScroll.Value += middleClickScrollingEndPoint.Y - middleClickScrollingStartPoint.Y;
                                };
                                Timer.Start();
                            }
                            else if (HorizontalScroll.Maximum > Scroll.ActualWidth)
                            {
                                isMiddleClickScrolling = true;
                                Cursor = new CoreCursor( CoreCursorType.SizeWestEast,1);
                                middleClickScrollingStartPoint = currentpoint.Position;
                                DispatcherTimer Timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
                                Timer.Tick += (a, b) =>
                                {
                                    if (!isMiddleClickScrolling)
                                    {
                                        ((DispatcherTimer)a).Stop();
                                    }
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

        private void TextControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);
            isSelecting = false;
            isLineSelect = false;
        }


        private void VerticalScroll_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Cursor = new CoreCursor(CoreCursorType.Arrow, 1);
        }

        private void VerticalScroll_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue)
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

        public List<SyntaxError> SyntaxErrors { get => Get(new List<SyntaxError>()); set { Set(value); CanvasScrollbarMarkers.Invalidate(); CanvasText.Invalidate(); } }
        public List<SearchMatch> SearchMatches { get => Get(new List<SearchMatch>()); set { Set(value); CanvasScrollbarMarkers.Invalidate(); } }
        private void Tbx_SearchChanged(object sender, TextChangedEventArgs e)
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
            for  (int iLine = 0; iLine < Lines.Count; iLine++)
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
                            SearchMatches.Add(new() { Match = text, iChar = nextindex, iLine = iLine  });
                            nextindex++;
                        }
                    }
                }
            }
            if (SearchMatches.Count > 0)
            {
                Selection = new SelectionRange(new(SearchMatches[0].iChar, SearchMatches[0].iLine));
            }
            CanvasScrollbarMarkers.Invalidate();
            CanvasSelection.Invalidate();
        }

        public class SearchMatch
        {
            public string Match { get; set; }
            public int iChar { get; set; }
            public int iLine { get; set; }
        }

        private void NormalArrowPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Cursor = new CoreCursor( CoreCursorType.Arrow,1);
        }

        private float scrollbarSize { get => (float)Application.Current.Resources["ScrollBarSize"]; }

        private void Btn_SearchClose(object sender, RoutedEventArgs e)
        {
            IsFindPopupOpen = false;
        }

        private void CanvasScrollbarMarkers_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (ShowScrollbarMarkers)
            {
                float width = (float)VerticalScroll.ActualWidth;
                float height = (float)CanvasScrollbarMarkers.ActualHeight;

                foreach (SearchMatch search in SearchMatches)
                    args.DrawingSession.DrawLine(width / 3f, search.iLine / (float)Lines.Count * height, width * 2/3f, search.iLine / (float)Lines.Count * height, ActualTheme == ElementTheme.Light ? Colors.LightGray.ChangeColorBrightness(-0.3f) : Colors.LightGray, 4f);

                foreach (SyntaxError error in SyntaxErrors)
                {
                    if (error.SyntaxErrorType == SyntaxErrorType.Error)
                    {
                        args.DrawingSession.DrawLine(width * 2/3f, error.iLine / (float)Lines.Count * height, width , error.iLine / (float)Lines.Count * height, ActualTheme == ElementTheme.Light ? Colors.Red.ChangeColorBrightness(-0.2f) : Colors.Red, 4f);
                    }
                    else if (error.SyntaxErrorType == SyntaxErrorType.Warning)
                    {
                        args.DrawingSession.DrawLine(width * 2 / 3f, error.iLine / (float)Lines.Count * height, width , error.iLine / (float)Lines.Count * height, ActualTheme == ElementTheme.Light ? Colors.Yellow.ChangeColorBrightness(-0.2f) : Colors.Yellow, 4f);
                    }
                }

                float cursorY = CursorPlace.iLine / (float)Lines.Count * height;
                args.DrawingSession.DrawLine(0, cursorY, width, cursorY, ActualTheme == ElementTheme.Light ? Color_Beam.InvertColorBrightness() : Color_Beam, 2f);
            }
        }

        int searchindex = 0;
        private void Btn_SearchNext(object sender, RoutedEventArgs e)
        {
            searchindex++;
            if (searchindex >= SearchMatches.Count)
            {
                searchindex = 0;
            }

            if (SearchMatches.Count > 0)
            {
                SearchMatch sm = SearchMatches[searchindex];
                Selection = new(new(sm.iChar, sm.iLine));
            }

        }

        private void TextControl_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            Debug.WriteLine(args.Data.Properties.Count);
        }

        private void Tbx_Search_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                Btn_SearchNext(null,null);
                e.Handled = true;
            }
        }
    }
}