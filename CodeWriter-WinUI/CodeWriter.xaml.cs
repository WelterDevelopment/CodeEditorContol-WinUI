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
        public static new readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSize", typeof(int), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).OnFontSizeChanged(d, e)));
        public static new readonly DependencyProperty RequestedThemeProperty = DependencyProperty.Register("RequestedTheme", typeof(ElementTheme), typeof(CodeWriter), new PropertyMetadata(12, (d, e) => ((CodeWriter)d).OnRequestedThemeChanged(d, e)));

        public Dictionary<Token, Color> TokensDark = new() { 
            { Token.Normal,         Color.FromArgb(255, 220, 220, 220) },
            { Token.Command,        Color.FromArgb(255, 40, 120, 200) },
            { Token.Environment,    Color.FromArgb(255, 40, 180, 140) },
            { Token.Comment,        Color.FromArgb(255, 40, 180, 80) },
            { Token.Key,            Color.FromArgb(255, 120, 120, 120) },
            { Token.Bracket,        Color.FromArgb(255, 80, 40, 180) },
            { Token.Reference,      Color.FromArgb(255, 180, 120, 40) },
            { Token.Math,           Color.FromArgb(255, 220, 160, 60) },
        };

        public Dictionary<Token, Color> TokensLight = new()
        {
            { Token.Normal,         Color.FromArgb(255, 20, 20, 20) },
            { Token.Command,        Color.FromArgb(255, 40, 120, 200) },
            { Token.Environment,    Color.FromArgb(255, 40, 180, 140) },
            { Token.Comment,        Color.FromArgb(255, 40, 180, 80) },
            { Token.Key,            Color.FromArgb(255, 120, 120, 120) },
            { Token.Bracket,        Color.FromArgb(255, 80, 40, 180) },
            { Token.Reference,      Color.FromArgb(255, 180, 120, 40) },
            { Token.Math,           Color.FromArgb(255, 220, 160, 60) },
        };

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(CodeWriter), new PropertyMetadata("", (d, e) => ((CodeWriter)d).OnTextChanged(d, e)));
        public bool needsInitialize = true;
        private Dictionary<string, object> _properties = new Dictionary<string, object>();
        private float CharOffset = 0;
        private ScrollBar HorizontalScroll;
        private int iCharOffset = 0;
        private bool invoked = false;
        private bool isCanvasLoaded = false;
        private bool IsSettingValue = false;
        private DateTime lastScroll = DateTime.Now;
        private bool middleClickScrollingActivated = false;
        private List<VirtualKey> Modifiers = new List<VirtualKey>();
        private bool pasting = false;
        private Point pos = new Point() { };
        private int startFontsize = 16;
        private ScrollBar VerticalScroll;

        public CodeWriter()
        {
            InitializeComponent();
            CharacterReceived += CodeWriter_CharacterReceived;
            CopyCommand = new RelayCommand(() => TextAction_Copy());
            PasteCommand = new RelayCommand(() => { Paste(); });
            DeleteCommand = new RelayCommand(() => TextAction_Delete());
            CutCommand = new RelayCommand(() => TextAction_Delete(true));
            Invalidate();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public enum ScrollOrientation
        {
            VerticalScroll,
            HorizontalScroll
        }

        public int CharHeight { get => Get(16); set { Set(value); } }
        public int CharWidth { get => Get(8); set { Set(value); } }
        public ICommand CopyCommand { get; set; }
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
                CanvasBeam.Invalidate();
            }
        }

        public ICommand CutCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public Color FoldingMarkerColor { get => Get(Color.FromArgb(255, 140, 140, 140)); set => Set(value); }
        public Color LeftColor { get => Get(Color.FromArgb(255, 40, 40, 40)); set => Set(value); }
        public Color PrimaryTextColor { get => Get(Color.FromArgb(255, 220, 220, 220)); set => Set(value); }
        public new int FontSize
        {
            get => (int)GetValue(FontSizeProperty);
            set { SetValue(FontSizeProperty, value); }
        }

        public int HorizontalOffset { get => Get(0); set { Set(value); } }
        public bool isLineSelect { get; private set; } = false;
        public bool isSelecting { get; private set; } = false;
        public bool IsSelection { get => Get(false); set => Set(value); }
        public int LeftIndent { get; private set; } = 0;
        public double LeftIndentLine { get; private set; } = 44;
        public int LeftWidth { get => (int)LineNumberWidth + (int)ErrorMarkerWidth + (int)WarningMarkerWidth + (int)FoldingMarkerWidth + TextIndent; }
        public Color LineNumberColor { get => Get(Color.FromArgb(255, 40, 140, 160)); set => Set(value); }
        public int LineNumberWidth { get => Get(12); set => Set(value); }
        public List<Line> Lines { get => Get(new List<Line>()); set => Set(value); }
        public int lineSelectFrom { get; private set; } = 0;
        public int MarkerWidth { get => Get(2); set => Set(value); }
        public bool mouseIsDrag { get; private set; } = false;
        public bool mouseIsDragDrop { get; private set; } = false;
        public ICommand PasteCommand { get; set; }
        public string SelectedText
        {
            get
            {
                string text = "";
                if (Selection.Start.iLine == Selection.End.iLine)
                {
                    text = Lines[Selection.Start.iLine].LineText.Substring(Selection.Start.iChar, Selection.End.iChar - Selection.Start.iChar);
                }
                else
                    for (int iLine = Selection.Start.iLine; iLine < Selection.End.iLine; iLine++)
                    {
                        if (iLine == Selection.Start.iLine)
                            text += Lines[iLine].LineText.Substring(Selection.Start.iChar) + "\n";
                        else if (iLine == Selection.End.iLine)
                            text += Lines[iLine].LineText.Substring(0, Selection.End.iChar);
                        else
                            text += Lines[iLine].LineText + "\n";
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
                if (value.Start == value.End)
                    Lines[value.Start.iLine].IsSelected = new SolidColorBrush(Colors.DarkGray);

                var width = Scroll.ActualWidth - LeftWidth;
                if (value.End.iChar * CharWidth < HorizontalScroll.Value)
                    HorizontalScroll.Value = value.End.iChar * CharWidth;
                else if ((value.End.iChar) * CharWidth - width - HorizontalScroll.Value > 0)
                    HorizontalScroll.Value = Math.Max(value.End.iChar * CharWidth - width, 0);

                if (value.End.iLine * CharHeight < VerticalScroll.Value)
                    VerticalScroll.Value = value.End.iLine * CharHeight;
                else if (value.End.iLine * CharHeight - Scroll.ActualHeight > VerticalScroll.Value)
                    VerticalScroll.Value = Math.Min(value.End.iLine * CharHeight - Scroll.ActualHeight, VerticalScroll.Maximum);

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

        public new ElementTheme RequestedTheme
        {
            get => (ElementTheme)GetValue(RequestedThemeProperty);
            set => SetValue(RequestedThemeProperty, value);
        }

        public List<Line> VisibleLines { get; set; } = new List<Line>();

        public new SolidColorBrush Background { get => Get(new SolidColorBrush(Color.FromArgb(255,10,10,10))); set => Set(value); }


        public bool WordWrap { get; private set; } = true;
        private int ErrorMarkerWidth { get => CharWidth / 2; }
        private int FoldingMarkerWidth { get => CharWidth; }
        private VirtualKeyModifiers lastModifiers { get; set; }
        private int TextIndent { get => CharWidth; }
        private int WarningMarkerWidth { get => CharWidth / 2; }
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

        private void CanvasBeam_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (VisibleLines.Count > 0)
            {
                int x = (int)(LeftWidth + HorizontalOffset + CursorPlace.iChar * CharWidth);
                int y = (int)((CursorPlace.iLine - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);

                if (y <= TextControl.ActualHeight && y >= 0 && x <= TextControl.ActualWidth && x >= LeftWidth)
                    args.DrawingSession.DrawLine(new Vector2(x, y), new Vector2(x, y + CharHeight), new CanvasSolidColorBrush(sender, Color.FromArgb(255, 200, 200, 200)), 2f);

                int xms = (int)(LeftWidth);
                int iCharStart = iCharOffset;
                int xme = (int)TextControl.ActualWidth;
                int iCharEnd = iCharStart + (int)((xme - xms) / CharWidth);

                for (int iChar = iCharStart; iChar < iCharEnd; iChar++)
                {
                    int xs = (int)((iChar - iCharStart) * CharWidth) + xms;
                    if (iChar % 10 == 0)
                        args.DrawingSession.DrawLine(xs, 0, xs, CharHeight / 8, new CanvasSolidColorBrush(sender, LineNumberColor), 2f);
                }

                if (Selection.Start == CursorPlace)
                {
                    args.DrawingSession.DrawRoundedRectangle(LeftWidth, y, (int)TextControl.ActualWidth - LeftIndent, CharHeight, 2, 2, FoldingMarkerColor, 1);
                }
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

        private void OnRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ElementTheme theme = (ElementTheme)e.NewValue;
            if (theme == ElementTheme.Light)
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 240, 240, 240));
                LeftColor = Color.FromArgb(255, 220, 220, 220);

            }
            else
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 35, 35, 35));
                LeftColor = Color.FromArgb(255, 25, 25, 25);

            }
            Invalidate();
           
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

                if (Selection.Start.iLine == Selection.End.iLine)
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
                }

                TextChanged();
                Invalidate();
            }
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

                Size size = MeasureTextSize(CanvasText.Device, "│", textFormat);
                Size sizew = MeasureTextSize(CanvasText.Device, "–", textFormat);

                CharHeight = (int)(size.Height * 1.03f);
                CharWidth = (int)(sizew.Width * 1.31f);
            }

            if (VerticalScroll != null && HorizontalScroll != null && Lines != null)
            {
                VerticalScroll.Maximum = (Lines.Count + 1) * CharHeight - Scroll.ActualHeight;
                VerticalScroll.SmallChange = CharHeight;
                VerticalScroll.LargeChange = 3 * CharHeight;

                int maxchars = 0;
                foreach (Line l in Lines)
                {
                    maxchars = l.Count > maxchars ? l.Count : maxchars;
                }

                HorizontalScroll.Maximum = (maxchars + 1) * CharWidth - Scroll.ActualWidth + LeftWidth;
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

                LineNumberWidth = CharWidth * IntLength(Lines.Count);

                CanvasBeam.Invalidate();
                CanvasSelection.Invalidate();
                CanvasText.Invalidate();
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

            int n = Math.Max((int)(e.NewValue / CharWidth) * CharWidth, 0);

            CharOffset = n;
            iCharOffset = (int)(n / CharWidth);
            HorizontalOffset = -n;
            CanvasBeam.Invalidate();
            CanvasSelection.Invalidate();
            CanvasText.Invalidate();
            // Invalidate();
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
            Invalidate();
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

        private Size MeasureTextSize(CanvasDevice device, string text, CanvasTextFormat textFormat, float limitedToWidth = 0.0f, float limitedToHeight = 0.0f)
        {
            CanvasTextLayout layout = new(device, text, textFormat, limitedToWidth, limitedToHeight);

            double width = layout.DrawBounds.Width;
            double height = layout.DrawBounds.Height;

            return new(width, height);
        }

        private void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => Invalidate();

        private void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d as CodeWriter).IsSettingValue)
            {
                InitializeLines((string)e.NewValue);
            }
        }

        private async void Paste()
        {
            if (!IsSelection)
            {
                string text = "";
                DataPackageView dataPackageView = Clipboard.GetContent();
                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    text += await dataPackageView.GetTextAsync();
                }
                Lines[CursorPlace.iLine].LineText = Lines[CursorPlace.iLine].LineText.Insert(CursorPlace.iChar, text);
                Place end = new(CursorPlace.iChar + text.Length, CursorPlace.iLine);
                Selection = new(Selection.Start, end);
                TextChanged();
                Invalidate();
            }
        }
        private Place PointerToPlace(Point currentpoint)
        {
            int iline = Math.Min((int)(currentpoint.Y / CharHeight) + VisibleLines[0].LineNumber - 1, Lines.Count - 1);
            int ichar = 0;
            if ((int)currentpoint.X - LeftWidth - HorizontalOffset > 0)
                ichar = Math.Min((int)((currentpoint.X - LeftWidth - HorizontalOffset + CharWidth * 2 / 3) / CharWidth), Lines[iline].Count);

            return new Place(ichar, iline);
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
                        if (CursorPlace.iChar < Lines[CursorPlace.iLine].Count)
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
                        if (!IsSelection)
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
                            Selection = new(newplace, newplace);
                        }
                        break;

                    case VirtualKey.Down:
                        if (CursorPlace.iLine < Lines.Count - 1)
                        {
                            Place newplace = CursorPlace;
                            newplace.iLine++;
                            newplace.iChar = Math.Min(Lines[newplace.iLine].Count, newplace.iChar);
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
                //FontSize += Math.Sign(mwd);
                int newfontsize = FontSize + Math.Sign(mwd);
                if (newfontsize >= 6 && newfontsize <= 36)
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

                FontSize = Math.Min(Math.Max((int)(startFontsize * e.Cumulative.Scale), 6), 36);
                HorizontalScroll.Value -= e.Delta.Translation.X;
                VerticalScroll.Value -= e.Delta.Translation.Y;
            }
        }

        private void ScrollContent_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            startFontsize = FontSize;
        }

        private void CanvasSelection_Draw(CanvasControl sender, CanvasDrawEventArgs args)
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
                            x = (int)(LeftWidth + HorizontalOffset + i * CharWidth);
                            y = (int)((CursorPlace.iLine - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                            DrawSelectionCell(args.DrawingSession, x, y);
                        }
                    else
                        for (int i = Selection.End.iChar; i < Selection.Start.iChar; i++)
                        {
                            x = (int)(LeftWidth + HorizontalOffset + i * CharWidth);
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
                                x = (int)(LeftWidth + HorizontalOffset + i * CharWidth);
                                y = (int)((lp - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                                DrawSelectionCell(args.DrawingSession, x, y);
                            }
                        else if (lp > start.iLine && lp < end.iLine)
                            for (int i = 0; i < Lines[lp].Count + 1; i++)
                            {
                                x = (int)(LeftWidth + HorizontalOffset + i * CharWidth);
                                y = (int)((lp - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                                DrawSelectionCell(args.DrawingSession, x, y);
                            }
                        else if (lp == end.iLine)
                            for (int i = 0; i < end.iChar; i++)
                            {
                                x = (int)(LeftWidth + HorizontalOffset + i * CharWidth);
                                y = (int)((lp - VisibleLines[0].LineNumber + 1) * CharHeight - 1 / 2 * CharHeight);
                                DrawSelectionCell(args.DrawingSession, x, y);
                            }
                }
            }
        }

        private void CanvasText_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
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
        }

        private void CanvasText_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (VisibleLines.Count > 0)
            {
                int foldPos = LineNumberWidth + ErrorMarkerWidth + WarningMarkerWidth;
                int errorPos = LineNumberWidth;
                int warningPos = LineNumberWidth + ErrorMarkerWidth;
                for (int iLine = VisibleLines[0].LineNumber - 1; iLine < VisibleLines.Last().LineNumber; iLine++)
                {
                    float y = CharHeight * (iLine - VisibleLines[0].LineNumber + 1);
                    args.DrawingSession.FillRectangle(0, y, LeftWidth - TextIndent, CharHeight, LeftColor);
                    args.DrawingSession.DrawText((iLine + 1).ToString(), CharWidth * IntLength(Lines.Count), y, LineNumberColor, new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize, HorizontalAlignment = CanvasHorizontalAlignment.Right });

                    if (Lines[iLine].IsFoldStart)
                    {
                        args.DrawingSession.DrawRectangle(foldPos, y + CharHeight / 4, CharWidth, CharWidth, FoldingMarkerColor, 2);
                        args.DrawingSession.DrawLine(foldPos + CharWidth / 4, y + CharHeight / 2, foldPos + CharWidth * 3 / 4, y + CharHeight / 2, FoldingMarkerColor, 2);
                    }
                    else if (Lines[iLine].IsFoldInner)
                    {
                        args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y, foldPos + CharWidth / 2, y + CharHeight, FoldingMarkerColor, 2);
                    }
                    else if (Lines[iLine].IsFoldInnerEnd)
                    {
                        args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y, foldPos + CharWidth / 2, y + CharHeight, FoldingMarkerColor, 2);
                        args.DrawingSession.DrawLine(foldPos + CharWidth / 2, y + CharHeight / 2, foldPos + CharWidth, y + CharHeight / 2, FoldingMarkerColor, 2);
                    }

                    if (Lines[iLine].IsError)
                    {
                        args.DrawingSession.FillRectangle(errorPos, y, ErrorMarkerWidth, CharHeight, Color.FromArgb(255, 200, 40, 40));
                    }

                    if (Lines[iLine].IsWarning)
                    {
                        args.DrawingSession.FillRectangle(warningPos, y, WarningMarkerWidth, CharHeight, Color.FromArgb(255, 180, 180, 40));
                    }

                    int lastChar = Math.Min(iCharOffset + (int)(Scroll.ActualWidth / CharWidth), Lines[iLine].Count);
                    for (int iChar = iCharOffset; iChar < lastChar; iChar++)
                    {
                        float x = LeftWidth + CharWidth * (iChar - iCharOffset);

                        Char c = Lines[iLine][iChar];
                        args.DrawingSession.DrawText(c.C.ToString(), x, y, RequestedTheme == ElementTheme.Light ? TokensLight[c.T] : TokensDark[c.T], new CanvasTextFormat() { FontFamily = "Consolas", FontSize = FontSize });
                        
                    }
                }
            }
        }

        private void TextChanged()
        {
            CanvasText.Invalidate();
            IsSettingValue = true;
            string t = string.Join("\n", Lines.Select(x => x.LineText));
            Text = t;
            IsSettingValue = false;
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
                else
                {
                    if (currentpoint.Position.X < LeftWidth)
                    {
                        Cursor = new CoreCursor(CoreCursorType.Hand, 1);
                    }
                    else
                    {
                        Cursor = new CoreCursor(CoreCursorType.IBeam, 1);
                    }
                }
            }
        }

        private void TextControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
            {
            }
            else if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse | e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
            {
                if (currentpoint.Properties.IsLeftButtonPressed)
                {
                    isLineSelect = currentpoint.Position.X < LeftWidth;
                    try
                    {
                        isSelecting = true;
                        if (!isLineSelect)
                        {
                            if (pos != currentpoint.Position)
                            {
                                Place start = PointerToPlace(currentpoint.Position);
                                Place end = PointerToPlace(currentpoint.Position);
                                Selection = new(start, end);
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
                    }
                    catch (Exception ex) { }
                }
                else if (currentpoint.Properties.IsMiddleButtonPressed)
                {
                }
            }
        }

        private void TextControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ExpPointerPoint currentpoint = e.GetCurrentPoint(TextControl);
            isSelecting = false;
            isLineSelect = false;
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
    }
}