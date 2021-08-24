using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
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
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace CodeWriter_WinUI
{
    public partial class CodeWriter : UserControl, INotifyPropertyChanged
    {
        public static new readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSize", typeof(int), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).OnFontSizeChanged(d, e)));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(CodeWriter), new PropertyMetadata("", (d, e) => ((CodeWriter)d).OnTextChanged(d, e)));
        public bool needsInitialize = true;
        private Dictionary<string, object> _properties = new Dictionary<string, object>();
        private float CharOffset = 0;
        private ScrollBar HorizontalScroll;
        private int iCharOffset = 0;
        private bool IsSettingValue = false;
        private DateTime lastScroll = DateTime.Now;
        private bool middleClickScrollingActivated = false;
        private List<VirtualKey> Modifiers = new List<VirtualKey>();
        bool isCanvasLoaded = false;
        private ScrollBar VerticalScroll;

        public CodeWriter()
        {
            Options = new CodeWriterOptions();
            InitializeComponent();
            CharacterReceived += CodeWriter_CharacterReceived;
            Invalidate();
        }

        public CodeWriter(CodeWriterOptions codeWriterViewModel)
        {
            Options = codeWriterViewModel;
            InitializeComponent();
            CharacterReceived += CodeWriter_CharacterReceived;
            Invalidate();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public enum ScrollOrientation
        {
            VerticalScroll,
            HorizontalScroll
        }

        public float CharHeight { get => Get(12f); set { Set(value); } }

        public float CharWidth { get; set; } = 8;

        public CoreCursor Cursor
        {
            get { return base.ProtectedCursor; }
            set { base.ProtectedCursor = value; }
        }

        public Place CursorPlace
        {
            get => Get(new Place()); set
            {
                Set(value);
                BeamCanvas.Invalidate();
            }
        }

        public new int FontSize
        {
            get => (int)GetValue(FontSizeProperty);
            set { SetValue(FontSizeProperty, value); Options.FontSize = value; }
        }

        public bool isLineSelect { get; private set; } = false;
        public bool isSelecting { get; private set; } = false;
        public bool IsSelection { get => Selection.Start.iChar != Selection.End.iChar | Selection.Start.iLine != Selection.End.iLine; }
        public int LeftIndent { get; private set; } = 0;
        public double LeftIndentLine { get; private set; } = 44;
        public Windows.UI.Color LineNumberColor { get => Get(Colors.DeepSkyBlue); set => Set(value); }
        public List<Line> Lines { get => Get(new List<Line>()); set => Set(value); }
        public int lineSelectFrom { get; private set; } = 0;

        public bool mouseIsDrag { get; private set; } = false;

        public bool mouseIsDragDrop { get; private set; } = false;
        public CodeWriterOptions Options { get; set; }

        public SelectionRange Selection
        {
            get => Get(new SelectionRange());
            set
            {
                Set(value);
                if (value.Start == value.End)
                    Lines[value.Start.iLine].IsSelected = new SolidColorBrush(Colors.DarkGray);

                var width = Scroll.ActualWidth - Options.LeftWidth;
                if (value.End.iChar * CharWidth < HorizontalScroll.Value)
                    HorizontalScroll.Value = value.End.iChar * CharWidth;
                else if ((value.End.iChar) * CharWidth - width - HorizontalScroll.Value > 0)
                    HorizontalScroll.Value = Math.Max(value.End.iChar * CharWidth - width, 0);

                if (value.End.iLine * CharHeight < VerticalScroll.Value)
                    VerticalScroll.Value = value.End.iLine * CharHeight;
                else if (value.End.iLine * CharHeight - Scroll.ActualHeight > VerticalScroll.Value)
                    VerticalScroll.Value = Math.Min(value.End.iLine * CharHeight - Scroll.ActualHeight, VerticalScroll.Maximum);

                CursorPlace = new Place(value.End.iChar, value.End.iLine);
                SelectionCanvas.Invalidate();
            }
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public List<Line> VisibleLines { get => Get(new List<Line>()); set => Set(value); }

        public bool WordWrap { get; private set; } = true;

        private VirtualKeyModifiers lastModifiers { get; set; }

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
            DrawText();
        }

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

        private void BeamCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (VisibleLines.Count > 0)
            {
                int x = (int)(Options.LeftWidth + Options.HorizontalOffset.Left + CursorPlace.iChar * CharWidth);
                int y = (int)((CursorPlace.iLine - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);

                if (y <= TextControl.ActualHeight && y >= 0 && x <= TextControl.ActualWidth && x >= Options.LeftWidth)
                    args.DrawingSession.DrawLine(new Vector2(x, y), new Vector2(x, y + CharHeight), new CanvasSolidColorBrush(sender, Color.FromArgb(255, 200, 200, 200)), 2f);
            }
        }

        private void CodeWriter_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
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

                Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, args.Character.ToString());
                CursorPlace = new(CursorPlace.iChar + 1, CursorPlace.iLine);
                TextChanged();
            }
        }

        private void Content_Loaded(object sender, RoutedEventArgs e)
        {
            Invalidate();
        }

        private void DrawSelectionCell(CanvasDrawingSession session, int x, int y)
        {
            session.FillRectangle(x, y, CharWidth, CharHeight, Color.FromArgb(120, 40, 40, 180));
        }

        private void DrawText()
        {
            if (isCanvasLoaded)
            {
                CanvasTextFormat textFormat = new CanvasTextFormat
                {
                    FontFamily = "Consolas",
                    FontSize = FontSize
                };

                Size size = MeasureTextSize(TextCancas.Device, "│", textFormat);
                Size sizew = MeasureTextSize(TextCancas.Device, "–", textFormat);

                CharHeight = (float)size.Height * 1.03f;
                CharWidth = (float)sizew.Width * 1.31f;
            }

            if (VerticalScroll != null && Lines != null)
            {
                VerticalScroll.Maximum = (Lines.Count + 1) * CharHeight - Scroll.ActualHeight;
                VerticalScroll.SmallChange = CharHeight;
                VerticalScroll.LargeChange = 3 * CharHeight;

                int maxchars = 0;
                foreach (Line l in Lines)
                {
                    maxchars = l.Count > maxchars ? l.Count : maxchars;
                }

                HorizontalScroll.Maximum = (maxchars + 1) * CharWidth - Scroll.ActualWidth + Options.LeftWidth;
                HorizontalScroll.SmallChange = CharWidth;
                HorizontalScroll.LargeChange = 3 * CharWidth;

                int StartLine = (int)(VerticalScroll.Value / CharHeight) + 1;
                int EndLine = Math.Min((int)((VerticalScroll.Value + Scroll.ActualHeight) / CharHeight), Lines.Count);
                VisibleLines.Clear();
                var vis = new List<Line>();
                foreach (Line l in Lines)
                {
                    if (l.LineNumber <= EndLine && l.LineNumber >= StartLine)
                    {
                        vis.Add(l);
                    }
                }
                VisibleLines = vis;

                Options.LineNumberWidth = new GridLength(CharWidth * IntLength(Lines.Count), GridUnitType.Pixel);
                Options.LeftMargin = new Thickness(Options.LeftWidth, 0, 0, 0);
                Options.FontSize = FontSize;
                Options.CharHeight = CharHeight;
                BeamCanvas.Invalidate();
                SelectionCanvas.Invalidate();
                TextCancas.Invalidate();
            }
        }

        private void HorizontalScroll_Loaded(object sender, RoutedEventArgs e)
        {
            HorizontalScroll = sender as ScrollBar;
        }

        private void HorizontalScroll_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue)
            {
                return;
            }
            float n = Math.Max((int)(e.NewValue / CharWidth - 1) * CharWidth, 0);
            CharOffset = n;
            iCharOffset = (int)(n / CharWidth);
            Options.HorizontalOffset = new Thickness(-n, 0, 0, 0);
            BeamCanvas.Invalidate();
            SelectionCanvas.Invalidate();
            TextCancas.Invalidate();
            // Invalidate();
        }

        private void InitializeLines(string text)
        {
            //  needsInitialize = false;
            Lines.Clear();
            string[] lines = text.Contains("\r\n") ? text.Split("\r\n", StringSplitOptions.None) : text.Split("\n", StringSplitOptions.None);
            int lineNumber = 1;
            foreach (string line in lines)
            {
                Line l = new Line() { LineNumber = lineNumber, LineText = line };
                Lines.Add(l);
                //IncrementalLines.Add(l);
                lineNumber++;
            }

            Invalidate();
        }

        private Windows.Foundation.Size MeasureTextSize(CanvasDevice device, string text, CanvasTextFormat textFormat, float limitedToWidth = 0.0f, float limitedToHeight = 0.0f)
        {

            var layout = new CanvasTextLayout(device, text, textFormat, limitedToWidth, limitedToHeight);

            var width = layout.DrawBounds.Width;
            var height = layout.DrawBounds.Height;

            return new(width, height);
        }

        private void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => Invalidate();

        private void OnMouseDown(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);

            if (currentpoint.Properties.IsLeftButtonPressed)
            {
                isLineSelect = currentpoint.Position.X < Options.LeftWidth;
                try
                {
                    RemoveAllHighlights();
                    isSelecting = true;
                    if (!isLineSelect)
                    {
                        Place start = PointerToPlace(currentpoint);
                        Place end = PointerToPlace(currentpoint);
                        Selection = new(start, end);
                    }
                    else
                    {
                        Place start = PointerToPlace(currentpoint);
                        Place end = new(Lines[start.iLine].Count, start.iLine);
                        Selection = new(start, end);
                    }
                }
                catch (Exception ex) { }
            }
            else
             if (currentpoint.Properties.IsMiddleButtonPressed)
            {
                //ActivateMiddleClickScrollingMode(e);
            }
            //e.Handled = true;
        }

        private void OnMouseUp(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);
            isSelecting = false;
            isLineSelect = false;
        }

        private void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d as CodeWriter).IsSettingValue)
            {
                InitializeLines((string)e.NewValue);
            }
        }

        private Place PointerToPlace(ExpPointerPoint currentpoint)
        {
            int iline = Math.Min((int)(currentpoint.Position.Y / CharHeight) + VisibleLines[0].LineNumber - 1, Lines.Count - 1);
            int ichar = 0;
            if ((int)currentpoint.Position.X - Options.LeftWidth - Options.HorizontalOffset.Left > 0)
                ichar = Math.Min((int)((currentpoint.Position.X - Options.LeftWidth - Options.HorizontalOffset.Left + CharWidth * 2 / 3) / CharWidth), Lines[iline].Count);

            return new Place(ichar, iline);
        }

        private void RemoveAllHighlights()
        {
            if (Lines != null && Lines.Count > 0)
                foreach (Line l in Lines)
                {
                    l.IsSelected = new SolidColorBrush(Colors.Transparent);
                }
        }

        private void Scroll_KeyDown(object sender, KeyRoutedEventArgs e)
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
                    case VirtualKey.Enter:
                        if (CursorPlace.iLine < Lines[CursorPlace.iLine].Count)
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
                        RemoveAllHighlights();
                        Selection = new SelectionRange(newselect, newselect);
                        Invalidate();
                        TextChanged();
                        break;

                    case VirtualKey.Delete:
                        if (CursorPlace.iChar == Lines[CursorPlace.iLine].Count && CursorPlace.iLine < Lines.Count)
                        {
                            storetext = Lines[CursorPlace.iLine + 1].LineText;
                            Lines.RemoveAt(CursorPlace.iLine + 1);
                            Lines[CursorPlace.iLine].LineText += storetext;
                            Invalidate();
                        }
                        else
                        {
                            Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar, 1);
                        }
                        TextChanged();

                        break;

                    case VirtualKey.Back:
                        if (IsSelection && Selection.Start.iLine == Selection.End.iLine)
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
                        else
                        {
                            if (CursorPlace.iChar == 0)
                            {
                                storetext = Lines[CursorPlace.iLine].LineText;
                                Lines.RemoveAt(CursorPlace.iLine);
                                Place newplace = CursorPlace;
                                newplace.iLine--;
                                newplace.iChar = Lines[newplace.iLine].Count;
                                Lines[newplace.iLine].LineText += storetext;
                                Selection = new SelectionRange(newplace);
                            }
                            else
                            {
                                Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Remove(CursorPlace.iChar - 1, 1);
                                Place newplace = CursorPlace;
                                newplace.iChar--;
                                Selection = new SelectionRange(newplace);
                            }
                        }
                        TextChanged();
                        break;

                    case VirtualKey.Up:
                        if (CursorPlace.iLine > 0)
                        {
                            Place newplace = CursorPlace;
                            newplace.iLine--;
                            newplace.iChar = Math.Min(Lines[newplace.iLine].Count, newplace.iChar);
                            RemoveAllHighlights();
                            Selection = new(newplace, newplace);
                        }
                        break;

                    case VirtualKey.Down:
                        if (CursorPlace.iLine < Lines.Count - 1)
                        {
                            Place newplace = CursorPlace;
                            newplace.iLine++;
                            newplace.iChar = Math.Min(Lines[newplace.iLine].Count, newplace.iChar);
                            RemoveAllHighlights();
                            Selection = new(newplace, newplace);
                        }
                        break;

                    case VirtualKey.Left:
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
                        break;

                    case VirtualKey.Right:
                        if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
                        {
                            Place newplace = CursorPlace;
                            newplace.iChar++;
                            Selection = new(newplace);
                        }
                        else if (CursorPlace.iLine < Lines.Count)
                        {
                            Place newplace = CursorPlace;
                            newplace.iLine++;
                            newplace.iChar = 0;
                            Selection = new(newplace);
                        }
                        break;
                }
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
                FontSize += Math.Sign(mwd);
            }
            e.Handled = true;
        }

        private void Scroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (VerticalScroll != null)
                Invalidate();
        }

        private void SelectionCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (VisibleLines.Count > 0)
            {
                int x = 0;
                int y = 0;
                if (Selection.Start.iLine == Selection.End.iLine)
                {
                    if (Selection.Start.iChar < Selection.End.iChar)
                        for (int i = Selection.Start.iChar; i < Selection.End.iChar; i++)
                        {
                            x = (int)(Options.LeftWidth + Options.HorizontalOffset.Left + i * CharWidth);
                            y = (int)((CursorPlace.iLine - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                            DrawSelectionCell(args.DrawingSession, x, y);
                        }
                    else
                        for (int i = Selection.End.iChar; i < Selection.Start.iChar; i++)
                        {
                            x = (int)(Options.LeftWidth + Options.HorizontalOffset.Left + i * CharWidth);
                            y = (int)((CursorPlace.iLine - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                            DrawSelectionCell(args.DrawingSession, x, y);
                        }
                }
                else
                {
                    Place start = new Place();
                    Place end = new Place();

                    if (Selection.Start.iLine < Selection.End.iLine)
                    {
                        start = Selection.Start;
                        end = Selection.End;
                    }
                    else
                    {
                        start = Selection.End;
                        end = Selection.Start;
                    }

                    for (int lp = start.iLine; lp <= end.iLine; lp++)
                        if (lp == start.iLine)
                            for (int i = start.iChar; i < Lines[lp].Count + 1; i++)
                            {
                                x = (int)(Options.LeftWidth + Options.HorizontalOffset.Left + i * CharWidth);
                                y = (int)((lp - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                                DrawSelectionCell(args.DrawingSession, x, y);
                            }
                        else if (lp > start.iLine && lp < end.iLine)
                            for (int i = 0; i < Lines[lp].Count + 1; i++)
                            {
                                x = (int)(Options.LeftWidth + Options.HorizontalOffset.Left + i * CharWidth);
                                y = (int)((lp - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                                DrawSelectionCell(args.DrawingSession, x, y);
                            }
                        else if (lp == end.iLine)
                            for (int i = 0; i < end.iChar; i++)
                            {
                                x = (int)(Options.LeftWidth + Options.HorizontalOffset.Left + i * CharWidth);
                                y = (int)((lp - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                                DrawSelectionCell(args.DrawingSession, x, y);
                            }
                }
            }
        }

        private void TextBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var ch = (sender as TextBlock).DataContext as Char;
        }

        private void TextCancas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (VisibleLines.Count > 0)

            {
                float LineNumberWidth = CharWidth * IntLength(Lines.Count);
                float FoldingMarkerWidth = CharWidth;
                float ErrorMarkerWidth = CharWidth / 2;
                for (int iLine = VisibleLines[0].LineNumber - 1; iLine < VisibleLines.Last().LineNumber; iLine++)
                {
                    float y = CharHeight * (iLine - VisibleLines[0].LineNumber + 1);
                    args.DrawingSession.FillRectangle(0, y, Options.LeftWidth, CharHeight, Color.FromArgb(255, 40, 40, 40));
                    args.DrawingSession.DrawText((iLine + 1).ToString(), CharWidth * IntLength(Lines.Count), y, Color.FromArgb(255, 40, 140, 200), new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize, HorizontalAlignment = CanvasHorizontalAlignment.Right });

                    if (Lines[iLine].IsFoldStart)
                    {
                        args.DrawingSession.DrawText("⯆", LineNumberWidth + FoldingMarkerWidth, y, Color.FromArgb(255, 40, 80, 140), new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize, HorizontalAlignment = CanvasHorizontalAlignment.Center });
                    }

                    if (Lines[iLine].IsError)
                    {
                        args.DrawingSession.FillRectangle(LineNumberWidth + FoldingMarkerWidth * 2, y, ErrorMarkerWidth, CharHeight, Color.FromArgb(255, 200, 40, 40));
                    }

                    if (Lines[iLine].IsWarning)
                    {
                        args.DrawingSession.FillRectangle(LineNumberWidth + FoldingMarkerWidth * 2 + ErrorMarkerWidth, y, ErrorMarkerWidth, CharHeight, Color.FromArgb(255, 180, 180, 40));
                    }

                    int lastChar = Math.Min(iCharOffset + (int)(Scroll.ActualWidth / CharWidth), Lines[iLine].Count);
                    for (int iChar = iCharOffset; iChar < lastChar; iChar++)
                    {
                        float x = Options.LeftWidth + CharWidth * (iChar - iCharOffset);

                        Char c = Lines[iLine][iChar];
                        args.DrawingSession.DrawText(c.C.ToString(), x, y, c.ForeGround, new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize });
                    }
                }
            }
        }

        private void TextChanged()
        {
            TextCancas.Invalidate();
            IsSettingValue = true;
            string t = string.Join("\n", Lines.Select(x => x.LineText));
            Text = t;
            IsSettingValue = false;
        }

        private void TextControl_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);

            if (isSelecting)
            {
                RemoveAllHighlights();
                if (!isLineSelect)
                {
                    Selection = new SelectionRange(Selection.Start, PointerToPlace(currentpoint));
                }
                else
                {
                    Place end = PointerToPlace(currentpoint);
                    end.iChar = Lines[end.iLine].Count;
                    Selection = new SelectionRange(Selection.Start, end);
                }
            }
            else
            {
                if (currentpoint.Position.X < Options.LeftWidth)
                {
                    Cursor = new CoreCursor(CoreCursorType.Hand, 1);
                }
                else
                {
                    Cursor = new CoreCursor(CoreCursorType.IBeam, 1);
                }
            }
        }

        private void VerticalScroll_Loaded(object sender, RoutedEventArgs e)
        {
            VerticalScroll = sender as ScrollBar;
        }

        private void VerticalScroll_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Cursor = new CoreCursor(CoreCursorType.Arrow, 1);
        }

        private void VerticalScroll_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            //if (DateTime.Now - lastScroll  < TimeSpan.FromMilliseconds(200))
            //    return;

            if (e.NewValue == e.OldValue)
            {
                return;
            }
            int updown = e.NewValue > e.OldValue ? -1 : 0;
            if (Math.Abs((int)e.NewValue - (VisibleLines[0].LineNumber + updown) * CharHeight) < CharHeight)
            {
                //VerticalScroll.Value = e.OldValue;
                return;
            }

            lastScroll = DateTime.Now;
            int delta = (int)(e.NewValue - e.OldValue);
            delta = (int)Math.Ceiling(1d * delta / CharHeight);
            //VerticalScroll.Value = Math.Max(VerticalScroll.Minimum, Math.Min(VerticalScroll.Maximum, newValue));
            int lines = (int)(delta / CharHeight);
            Invalidate();
        }

        private void TextCancas_Loaded(object sender, RoutedEventArgs e)
        {
           
        }

        private void TextCancas_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Consolas",
                FontSize = FontSize
            };

            Size size = MeasureTextSize(sender.Device, "│", textFormat);
            Size sizew = MeasureTextSize(sender.Device, "–", textFormat);

            CharHeight = (float)size.Height * 1.03f;
            CharWidth = (float)sizew.Width * 1.31f;
            isCanvasLoaded = true;
        }
    }
}