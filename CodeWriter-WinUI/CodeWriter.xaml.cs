using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input.Experimental;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Protection;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace CodeWriter_WinUI
{
    
    public enum VisibleState : byte
    {
        Visible, StartOfHiddenBlock, Hidden
    }

    public static class Extensions
    {
        public static Vector2 Center(this Rect rect)
        {
            return new Vector2((float)rect.X + (float)rect.Width / 2, (float)rect.Y + (float)rect.Height / 2);
        }

        public static System.Drawing.Point ToDrawingPoint(this Windows.Foundation.Point point)
        {
            return new System.Drawing.Point((int)point.X, (int)point.Y);
        }

        public static Windows.Foundation.Point ToFoundationPoint(this System.Drawing.Point point)
        {
            return new Windows.Foundation.Point(point.X, point.Y);
        }

        public static Windows.UI.Color ToUIColor(this System.Drawing.Color color)
        {
            return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        public static Vector2 ToVector2(this System.Drawing.Point point)
        {
            return new Vector2((float)point.X, (float)point.Y);
        }
    }

    public static class RichEditBoxExtensions
    {
        public static readonly DependencyProperty BindableInlinesProperty =
            DependencyProperty.RegisterAttached("BindableInlines", typeof(ObservableCollection<Line>), typeof(RichEditBoxExtensions), new PropertyMetadata(null, OnBindableInlinesChanged));

        public static ObservableCollection<Line> GetBindableInlines(DependencyObject obj)
        {
            return (ObservableCollection<Line>)obj.GetValue(BindableInlinesProperty);
        }

        public static void SetBindableInlines(DependencyObject obj, ObservableCollection<Line> value)
        {
            obj.SetValue(BindableInlinesProperty, value);
        }
        private static void OnBindableInlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var Target = d as RichEditBox;

            if (Target != null && e.NewValue != e.OldValue)
            {
                Target.Document.SetText(TextSetOptions.None, "");
                Target.Document.Selection.StartPosition = 0;
                foreach (Line line in (ObservableCollection<Line>)e.NewValue)
                {
                    foreach (CharElement inline in line.CharGroups)
                    {
                        switch (inline)
                        {
                            case Char c:
                                if (c.C != '\n')
                                {
                                    //Run r = new Run() { Text = c.C.ToString(), Foreground = c.ForeGround };
                                    // c.R.Text = c.C.ToString();
                                    Target.Document.Selection.CharacterFormat.ForegroundColor = c.ForeGround;
                                    Target.Document.Selection.TypeText(c.C.ToString());
                                }
                                else
                                    Target.Document.Selection.TypeText("\n");

                                break;

                                // case CharGroup c: Target.Inlines.Add(new Run() { Text = new string(c.C.Select(x => x.C).ToArray()), Foreground = c.ForeGround }); break;
                        }
                    }
                    Target.Document.Selection.TypeText("\n");
                }
            }
        }
    }

    public static class TextBlockExtensions
    {
        public static readonly DependencyProperty BindableInlinesProperty =
            DependencyProperty.RegisterAttached("BindableInlines", typeof(IEnumerable<CharElement>), typeof(TextBlockExtensions), new PropertyMetadata(null, OnBindableInlinesChanged));

        public static IEnumerable<CharElement> GetBindableInlines(DependencyObject obj)
        {
            return (IEnumerable<CharElement>)obj.GetValue(BindableInlinesProperty);
        }

        public static void SetBindableInlines(DependencyObject obj, IEnumerable<CharElement> value)
        {
            obj.SetValue(BindableInlinesProperty, value);
        }
        private static void OnBindableInlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var Target = d as TextBlock;

            if (Target != null && e.NewValue != e.OldValue)
            {
                Target.Inlines.Clear();
                foreach (CharElement inline in (IEnumerable<CharElement>)e.NewValue)
                {
                    switch (inline)
                    {
                        case Char c:
                            if (c.C == '\t')
                            {
                                Run r = new Run() { Text = " ", Foreground = new SolidColorBrush(c.ForeGround) };
                                Target.Inlines.Add(r);
                            }
                            else if (c.C != '\n')
                            {
                                Run r = new Run() { Text = c.C.ToString(), Foreground = new SolidColorBrush( c.ForeGround) };
                                // c.R.Text = c.C.ToString();
                                Target.Inlines.Add(r);
                            }
                            else
                                Target.Inlines.Add(new LineBreak() { });

                            break;

                        case CharGroup c: Target.Inlines.Add(new Run() { Text = new string(c.C.Select(x => x.C).ToArray()), Foreground = new SolidColorBrush(c.ForeGround) }); break;
                    }
                }
            }
        }
    }

    public class Char : CharElement
    {
        public Char(char c)
        {
            C = c;
        }
    }

    public class CharElement : Bindable
    {
        public char C { get => Get(' '); set => Set(value); }

        public Color ForeGround { get => Get(Colors.White); set { Set(value); } }
       
        public Run R { get => Get(new Run() { Text = C.ToString() }); set => Set(value); }
    }

    public class CharGroup : CharElement
    {
        public CharGroup()
        {
        }

        public new Char[] C { get => Get(new Char[] { }); set => Set(value); }
    }

    public partial class CodeWriter : UserControl, INotifyPropertyChanged
    {
        public static new readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSize", typeof(int), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).OnFontSizeChanged(d, e)));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(CodeWriter), new PropertyMetadata("", (d, e) => ((CodeWriter)d).OnTextChanged(d, e)));
        public bool needsInitialize = true;
        private Dictionary<string, object> _properties = new Dictionary<string, object>();
        private ScrollBar HorizontalScroll;
        private bool IsSettingValue = false;
        private DateTime lastScroll = DateTime.Now;
        private bool middleClickScrollingActivated = false;
        private List<VirtualKey> Modifiers = new List<VirtualKey>();

        private ScrollBar VerticalScroll;

        public CodeWriter()
        {
            InitializeComponent();
            Resources.TryGetValue("CWVM", out object Vm);
            if (Vm != null)
            {
                Options = Vm as CodeWriterOptions;
            }
            else
                Options = new CodeWriterOptions();
            CharacterReceived += CodeWriter_CharacterReceived;
            Invalidate();
        }

        public CodeWriter(CodeWriterOptions codeWriterViewModel)
        {
            Options = codeWriterViewModel;
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public enum ScrollOrientation
        {
            VerticalScroll,
            HorizontalScroll
        }

        public float CharHeight { get => Get(12f); set { Set(value); } }

        public float CharWidth { get; set; } = 8;

        public CodeWriterOptions Options { get; set; }

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

        public Range draggedRange { get; private set; }

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

        //public IncrementalLoadingCollection<LinesSource, Line> IncrementalLines { get => Get(new IncrementalLoadingCollection<LinesSource, Line>()); set => Set(value); }
        public bool mouseIsDrag { get; private set; } = false;

        public bool mouseIsDragDrop { get; private set; } = false;

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

        public bool VirtualSpace { get; private set; }

        public List<Line> VisibleLines { get => Get(new List<Line>()); set => Set(value); }

        public bool WordWrap { get; private set; } = true;

        private VirtualKeyModifiers lastModifiers { get; set; }

        public Char this[Place place]
        {
            get => Lines[place.iLine][place.iChar];
            set => Lines[place.iLine][place.iChar] = value;
        }

        /// <summary>
        /// Gets Line
        /// </summary>
        public Line this[int iLine]
        {
            get { return Lines[iLine]; }
        }
        public static int IntLength(int i)
        {
            if (i < 0)
                throw new ArgumentOutOfRangeException();
            if (i == 0)
                return 1;
            return (int)Math.Floor(Math.Log10(i)) + 1;
        }

        public void Invalidate()
        {
            DrawText();
        }

        public void OnScroll(ScrollOrientation scrollOrientation, ScrollEventType se, int value, bool alignByLines)
        {
            //HideHints();

            if (scrollOrientation == ScrollOrientation.VerticalScroll)
            {
                //align by line height
                int newValue = value;
                if (alignByLines)
                    newValue = (int)(Math.Ceiling(1d * newValue / CharHeight) * CharHeight);
                //
                //VerticalScroll.Value = Math.Max(VerticalScroll.Minimum, Math.Min(VerticalScroll.Maximum, newValue));
            }
            //if (scrollOrientation == ScrollOrientation.HorizontalScroll)
            //    HorizontalScroll.Value = Math.Max(HorizontalScroll.Minimum, Math.Min(HorizontalScroll.Maximum, value));

            //UpdateScrollbars();

            // RestoreHints();

            Invalidate();
            //
            // base.OnScroll(se);
            //OnVisibleRangeChanged();
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
            if (name != "Blocks")
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

        private void DoScrollVertical(int countLines, int direction, int oldScrollValue = 0)
        {
            if (VerticalScroll.Visibility == Visibility.Visible)
            {
                int height = (int)ActualHeight;
                int numberOfVisibleLines = (int)(height / CharHeight);

                int offset;
                if ((countLines == -1) || (countLines > numberOfVisibleLines))
                    offset = (int)CharHeight * numberOfVisibleLines;
                else
                    offset = (int)CharHeight * countLines;

                int newScrollPos = oldScrollValue + (Math.Sign(direction) * offset);

                OnScroll(ScrollOrientation.VerticalScroll, direction > 0 ? ScrollEventType.SmallDecrement : ScrollEventType.SmallIncrement, newScrollPos, true);
            }
        }

        private void DrawSelectionCell(CanvasDrawingSession session, int x, int y)
        {
            session.FillRectangle(x, y, CharWidth, CharHeight, Color.FromArgb(120, 40, 40, 180));
        }

        private void DrawText()
        {
            CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Consolas",
                FontSize = FontSize,
                WordWrapping = CanvasWordWrapping.WholeWord,
            };

            Size size = MeasureTextSize("│", textFormat);
            Size sizew = MeasureTextSize("–", textFormat);

            CharHeight = (float)size.Height * 1.03f; //(FontSize * 1.2f);
            CharWidth = (float)sizew.Width * 1.31f; //(FontSize * 565f / 1024f);

          
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
                //VisibleLines = new ObservableCollection<Line>(Lines.Where(x => x.LineNumber <= EndLine && x.LineNumber >= StartLine));

                //foreach (Line l in VisibleLines)
                //{
                //    //l.LineText = l.LineText;
                //    //App.VM.Log(l.LineText +string.Join( "",l.Inlines.Select(x=>((Run)x).Text)));
                //    if (l.CharGroups.Count > MaxChars)
                //        l.CharGroups.Insert(MaxChars, new CharElement)
                //}
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

        private float CharOffset = 0;
        private int iCharOffset = 0;

        private void HorizontalScroll_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue)
            {
                return;
            }
            float n = Math.Max((int)(e.NewValue / CharWidth - 1) * CharWidth, 0);
            CharOffset = n;
            iCharOffset = (int) (n / CharWidth);
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

        private Windows.Foundation.Size MeasureTextSize(string text, CanvasTextFormat textFormat, float limitedToWidth = 0.0f, float limitedToHeight = 0.0f)
        {
            var device = CanvasDevice.GetSharedDevice();

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
            int iline = Math.Min((int)(currentpoint.Position.Y / CharHeight) + VisibleLines[0].LineNumber - 1, Lines.Count-1);
            int ichar = 0;
            if ((int)currentpoint.Position.X - Options.LeftWidth - Options.HorizontalOffset.Left > 0)
                 ichar = Math.Min((int)((currentpoint.Position.X - Options.LeftWidth - Options.HorizontalOffset.Left + CharWidth *2/3) / CharWidth ) , Lines[iline].Count);

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
                             storetext = Lines[CursorPlace.iLine+1].LineText;
                            Lines.RemoveAt(CursorPlace.iLine+1);
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

        private  void TextChanged()
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

        private void Content_Loaded(object sender, RoutedEventArgs e)
        {
            Invalidate();
        }

        private void TextCancas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (VisibleLines.Count > 0)

            {
                float LineNumberWidth = CharWidth * IntLength(Lines.Count);
                float FoldingMarkerWidth =  CharWidth;
                float ErrorMarkerWidth = CharWidth/2;
                for (int iLine = VisibleLines[0].LineNumber - 1; iLine < VisibleLines.Last().LineNumber; iLine++)
                {
                    float y = CharHeight * (iLine - VisibleLines[0].LineNumber + 1);
                    args.DrawingSession.FillRectangle(0, y, Options.LeftWidth, CharHeight, Color.FromArgb(255, 40, 40, 40));
                    args.DrawingSession.DrawText((iLine+1).ToString(), CharWidth * IntLength(Lines.Count), y, Color.FromArgb(255, 40, 140, 200), new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize, HorizontalAlignment = CanvasHorizontalAlignment.Right });

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
                        args.DrawingSession.FillRectangle(LineNumberWidth + FoldingMarkerWidth * 2+ErrorMarkerWidth, y, ErrorMarkerWidth, CharHeight, Color.FromArgb(255, 180, 180, 40));
                    }

                    int lastChar = Math.Min(iCharOffset + (int)(Scroll.ActualWidth/CharWidth), Lines[iLine].Count);
                    for (int iChar = iCharOffset; iChar < lastChar; iChar++)
                    {
                        float x = Options.LeftWidth + CharWidth * (iChar - iCharOffset);

                        Char c = Lines[iLine][iChar];
                        args.DrawingSession.DrawText(c.C.ToString(), x, y, c.ForeGround, new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize});
                    }
                }

            }
        }
    }

    public class CodeWriterOptions : Bindable
    {
        public double CharHeight { get => Get(16d); set => Set(value); }
        public GridLength ErrorWidth { get => Get(new GridLength(LineNumberWidth.Value, GridUnitType.Pixel)); set => Set(value); }
        public int FoldingMarkerWidth { get => Get((int)FontSize / 2); set { Set(value); } }
        public GridLength FoldingWidth { get => Get(new GridLength(FontSize, GridUnitType.Pixel)); set => Set(value); }
        public int FontSize { get => Get(16); set { Set(value); FoldingWidth = new(value); FoldingMarkerWidth = (int)value / 2; } }
        public Thickness HorizontalOffset { get => Get(new Thickness(0, 0, 0, 0)); set { Set(value); } }
        public Thickness LeftMargin { get => Get(new Thickness(LeftWidth, 0, 0, 0)); set => Set(value); }
        public int LeftWidth { get => (int)LineNumberWidth.Value + (int)ErrorWidth.Value + (int)FoldingWidth.Value + (int)MarkerWidth.Value; }
        public SolidColorBrush LineNumberColor { get => Get(new SolidColorBrush(Colors.DeepSkyBlue)); set => Set(value); }
        public GridLength LineNumberWidth { get => Get(new GridLength(12, GridUnitType.Pixel)); set => Set(value); }
        public GridLength MarkerWidth { get => Get(new GridLength(2, GridUnitType.Pixel)); set => Set(value); }
    }
    public class Folding : Bindable
    {
        public int Endline { get => Get(0); set => Set(value); }
        public int StartLine { get => Get(0); set => Set(value); }
    }

    public class HighlightRange
    {
        public Place End { get; set; }
        public Place Start { get; set; }
    }

    public class Line : Bindable
    {
        public VisibleState VisibleState = VisibleState.Visible;
        internal int wordWrapIndent = 0;
        public ObservableCollection<CharElement> CharGroups { get => Get(new ObservableCollection<CharElement>()); set => Set(value); }
        public ObservableCollection<Char> Chars { get => Get(new ObservableCollection<Char>()); set => Set(value); }
        public int Count
        {
            get { return Chars.Count; }
        }

        public bool IsError { get => Get(false); set => Set(value); }
        public bool IsWarning { get => Get(false); set => Set(value); }
        public string ErrorText { get => Get(""); set => Set(value); }
        public string WarningText { get => Get(""); set => Set(value); }
        public Folding Folding { get => Get(new Folding()); set => Set(value); }
        public string FoldingEndMarker { get; set; }
        public string FoldingStartMarker { get; set; }
        public IEnumerable<Inline> Inlines { get => Get(new ObservableCollection<Inline>()); set => Set(value); }
        public bool IsChanged { get; set; }
        public bool IsFoldEnd { get => Get(false); set => Set(value); }
        public bool IsFoldInner { get => Get(false); set => Set(value); }
        public bool IsFoldInnerEnd { get => Get(false); set => Set(value); }
        public bool IsFoldStart { get => Get(false); set => Set(value); }
        public bool IsReadOnly
        {
            get { return false; }
        }

        public SolidColorBrush IsSelected { get => Get(new SolidColorBrush(Colors.Transparent)); set => Set(value); }
        public DateTime LastVisit { get; set; }
        public int LineNumber { get => Get(0); set => Set(value); }
        public string LineText
        {
            get => Get("");
            set
            {
                Set(value);
                Chars = FormattedText(value);
                //CharGroups = FormattedText(value);
                IsFoldStart = FoldableStart(value);
                IsFoldInnerEnd = FoldableEnd(value);
                IsFoldInner = !IsFoldStart && !IsFoldInnerEnd;
                IsError = Error(value);
                IsWarning = Warning(value);
                Inlines = new ObservableCollection<Inline>(value.Select(x => new Run() { Text = x.ToString() }));
            }
        }

        public bool Marker { get => Get(false); set => Set(value); }
        public SolidColorBrush MarkerColor { get => Get(new SolidColorBrush(Colors.ForestGreen)); set => Set(value); }
        public int startY { get => Get(0); set => Set(value); }
        public int WordWrapStringsCount { get; internal set; }

        public Char this[int index]
        {
            get
            {
                return Chars[index];
            }
            set
            {
                Chars[index] = value;
            }
        }

        public void Add(Char item)
        {
            Chars.Add(item);
        }

        public virtual void AddRange(IEnumerable<Char> collection)
        {
            //Chars.AddRange(collection);
        }

        public void Clear()
        {
            Chars.Clear();
        }

        public bool Contains(Char item)
        {
            return Chars.Contains(item);
        }

        public void CopyTo(Char[] array, int arrayIndex)
        {
            Chars.CopyTo(array, arrayIndex);
        }

        private bool Error(string text)
        {
            bool error = false;
            error = text.Count(x => x == '[') != text.Count(x => x == ']');
            ErrorText = "Line does not contain the same number of opening and closing brackets";
            return error;
        }

        private bool Warning(string text)
        {
            bool error = false;
            error = text.Count() == 6;
            WarningText = "Line contains 6 characters!";
            return error;
        }

        public ObservableCollection<Char> FormattedText(string text)
        {
            List<Char> groups = new();

            groups = text.Select(x => new Char(x)).ToList();

            MatchCollection math = Regex.Matches(text, @"\$.*?\$");
            foreach (Match match in math)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].ForeGround = Color.FromArgb(255, 220, 160, 60);
                }
            }

            MatchCollection options = Regex.Matches(text, @"(\w+?\s*?)(=)");
            foreach (Match optionmatch in options)
            {
                for (int i = optionmatch.Groups[0].Captures[0].Index; i < optionmatch.Groups[0].Captures[0].Index + optionmatch.Groups[0].Captures[0].Length - 1; i++)
                {
                    groups[i].ForeGround = Color.FromArgb(255, 120, 120, 120);
                }
            }

            MatchCollection commands = Regex.Matches(text, @"\\.+?\b");
            foreach (Match match in commands)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].ForeGround = Windows.UI.Color.FromArgb(255, 40, 120, 200);
                }
            }

            MatchCollection startstops = Regex.Matches(text, @"\\(start|stop).+?\b");
            foreach (Match match in startstops)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].ForeGround = Color.FromArgb(255, 40, 180, 140);
                }
            }

            MatchCollection brackets = Regex.Matches(text, @"(?<!\\)\[|(?<!\\)\]");
            foreach (Match match in brackets)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].ForeGround = Color.FromArgb(255, 80, 40, 180);
                }
            }

            MatchCollection braces = Regex.Matches(text, @"(?<!\\)\{|(?<!\\)\}");
            foreach (Match match in braces)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].ForeGround = Color.FromArgb(255, 120, 80, 220);
                }
            }

            MatchCollection refs = Regex.Matches(text, @"(sec|eq|tab|fig):\w+");
            foreach (Match match in refs)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].ForeGround =Color.FromArgb(255, 180, 120, 40);
                }
            }

            MatchCollection linecomment = Regex.Matches(text, @"\%.*");
            foreach (Match match in linecomment)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].ForeGround = Color.FromArgb(255, 40, 180, 80);
                }
            }

            //int lastindex = 0;
            //while (groups.Count - lastindex > CodeWriter.MaxChars)
            //{
            //    groups.Insert(CodeWriter.MaxChars + lastindex, new Char('\n'));
            //    lastindex += CodeWriter.MaxChars + 1;
            //}

            return new(groups);
        }

        public IEnumerator<Char> GetEnumerator()
        {
            return Chars.GetEnumerator();
        }

        public int IndexOf(Char item)
        {
            return Chars.IndexOf(item);
        }

        public void Insert(int index, Char item)
        {
            Chars.Insert(index, item);
        }

        public bool Remove(Char item)
        {
            return Chars.Remove(item);
        }

        public void RemoveAt(int index)
        {
            Chars.RemoveAt(index);
        }

        public virtual void RemoveRange(int index, int count)
        {
            if (index >= Count)
                return;
            //Chars.RemoveRange(index, Math.Min(Count - index, count));
        }

        public virtual void TrimExcess()
        {
            // Chars.TrimExcess();
        }

        internal void ClearFoldingMarkers()
        {
        }

        internal int GetWordWrapStringFinishPosition(int v, Line line)
        {
            return 0;
        }

        internal int GetWordWrapStringIndex(int iChar)
        {
            return 0;
        }

        internal int GetWordWrapStringStartPosition(object v)
        {
            return 0;
        }

        private bool FoldableEnd(string text)
        {
            var match = Regex.Match(text, @"\\(stop).+?\b");
            if (match.Success)
            {
                return true;
            }
            else return false;
        }

        private bool FoldableStart(string text)
        {
            var match = Regex.Match(text, @"\\(start).+?\b");
            if (match.Success)
            {
                Folding = new Folding() { StartLine = LineNumber, Endline = LineNumber + 3 };
                return true;
            }
            else return false;
        }
    }
    public class Place : IEquatable<Place>
    {
        public int iChar = 0;
        public int iLine = 0;

        public Place()
        {
        }

        public Place(int iChar, int iLine)
        {
            this.iChar = iChar;
            this.iLine = iLine;
        }

        public static Place Empty
        {
            get { return new Place(); }
        }

        public static bool operator !=(Place p1, Place p2)
        {
            return !p1.Equals(p2);
        }

        public static Place operator +(Place p1, Place p2)
        {
            return new Place(p1.iChar + p2.iChar, p1.iLine + p2.iLine);
        }

        public static bool operator <(Place p1, Place p2)
        {
            if (p1.iLine < p2.iLine) return true;
            if (p1.iLine > p2.iLine) return false;
            if (p1.iChar < p2.iChar) return true;
            return false;
        }

        public static bool operator <=(Place p1, Place p2)
        {
            if (p1.Equals(p2)) return true;
            if (p1.iLine < p2.iLine) return true;
            if (p1.iLine > p2.iLine) return false;
            if (p1.iChar < p2.iChar) return true;
            return false;
        }

        public static bool operator ==(Place p1, Place p2)
        {
            return p1.Equals(p2);
        }

        public static bool operator >(Place p1, Place p2)
        {
            if (p1.iLine > p2.iLine) return true;
            if (p1.iLine < p2.iLine) return false;
            if (p1.iChar > p2.iChar) return true;
            return false;
        }

        public static bool operator >=(Place p1, Place p2)
        {
            if (p1.Equals(p2)) return true;
            if (p1.iLine > p2.iLine) return true;
            if (p1.iLine < p2.iLine) return false;
            if (p1.iChar > p2.iChar) return true;
            return false;
        }

        public bool Equals(Place other)
        {
            return iChar == other.iChar && iLine == other.iLine;
        }

        public override bool Equals(object obj)
        {
            return (obj is Place) && Equals((Place)obj);
        }

        public override int GetHashCode()
        {
            return iChar.GetHashCode() ^ iLine.GetHashCode();
        }

        public void Offset(int dx, int dy)
        {
            iChar += dx;
            iLine += dy;
        }
        public override string ToString()
        {
            return "(" + (iLine + 1) + "," + (iChar + 1) + ")";
        }
    }
    public class SelectionRange : Bindable
    {
        public SelectionRange(Place place)
        {
            Start = place;
            End = place;
        }

        public SelectionRange(Place start, Place end)
        {
            Start = start;
            End = end;
        }

        public SelectionRange()
        {
        }

        public Place End { get => Get(new Place()); set => Set(value); }
        public Place Start { get => Get(new Place()); set => Set(value); }
    }
}