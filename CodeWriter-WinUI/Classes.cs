using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Windows.Foundation;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CodeWriter_WinUI
{
    public class Bindable : INotifyPropertyChanged
    {
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

        protected void Set<T>(T value, [CallerMemberName] string name = null)
        {
            if (Equals(value, Get<T>(value, name)))
                return;
            _properties[name] = value;
            OnPropertyChanged(name);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

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

        public static Color ChangeColorBrightness(this Color color, float correctionFactor)
        {
            float red = color.R;
            float green = color.G;
            float blue = color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }

        public static Color InvertColorBrightness(this Color color)
        {
            // ToDo: Come up with some fancy way of producing perfect colors for the light theme
            float red = color.R;
            float green = color.G;
            float blue = color.B;

            float lumi = (0.33f * red) + (0.33f * green) + (0.33f * blue);

            red = 255 - lumi + 0.6f * (red - lumi);
            green = 255 - lumi + 0.35f * (green - lumi);
            blue = 255 - lumi + 0.4f * (blue - lumi);

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }
    }

    public class CharElement : Bindable
    {
        public char C { get => Get(' '); set => Set(value); }
        //public Color ForeGround { get => Get(Colors.White); set { Set(value); } }
        public Token T { get => Get(Token.Normal); set => Set(value); }
    }

    public class Char : CharElement
    {
        public Char(char c)
        {
            C = c;
        }
    }

    public class CharGroup : CharElement
    {
        public CharGroup(Char[] chars) 
        {
            C = chars;
        }

        public new Char[] C { get => Get(new Char[] { }); set => Set(value); }
    }

    public class CodeWriterOptions : Bindable
    {
       
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
       
        public List<Char> Chars { get => Get(new List<Char>()); set => Set(value); }

        public int Count
        {
            get { return Chars.Count; }
        }

        public int Indents
        {
            get { return LineText.Count(x=>x == '\t'); }
        }

        public Folding Folding { get => Get(new Folding()); set => Set(value); }
        public string FoldingEndMarker { get; set; }
        public string FoldingStartMarker { get; set; }
        public bool IsUnsaved { get => Get(false); set { Set(value); if (!value) lastsavedtext = LineText; } }
        public bool IsFoldEnd { get => Get(false); set => Set(value); }
        public bool IsFoldInner { get => Get(false); set => Set(value); }
        public bool IsFoldInnerEnd { get => Get(false); set => Set(value); }
        public bool IsFoldStart { get => Get(false); set => Set(value); }
        public int LineNumber { get => Get(0); set => Set(value); }
        string lastsavedtext = "";
        private bool initialized = false;
       
        public string LineText
        {
            get => Get("");
            set
            {
                if (!initialized)
                {
                    initialized = true;
                }
                else
                {
                    IsUnsaved = value != lastsavedtext;
                }
                
                Set(value);
                Chars = FormattedText(value);
                IsFoldStart = FoldableStart(value);
                IsFoldInnerEnd = FoldableEnd(value);
                IsFoldInner = !IsFoldStart && !IsFoldInnerEnd;
            }
        }

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

        public List<Char> FormattedText(string text)
        {
            List<Char> groups = new();

            groups = text.Select(x => new Char(x)).ToList();

            foreach (var token in Languages.ConTeXt)
            {
                MatchCollection mc = Regex.Matches(text, token.Value);
                foreach (Match match in mc)
                {
                    for (int i = match.Index; i < match.Index + match.Length; i++)
                    {
                        groups[i].T = token.Key;
                    }
                }
            }

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

        public Place(Place oldplace)
        {
            this.iChar = oldplace.iChar;
            this.iLine = oldplace.iLine;
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

    public class BracketPair
    {
        public Place iOpen { get; set; } = new Place();
        public Place iClose { get; set; } = new Place();

        public BracketPair() { }
        public BracketPair(Place open, Place close) 
        {
            iOpen = open;
            iClose = close;
        }
    }

    public class SelectionRange : Bindable
    {
        public SelectionRange(Place place)
        {
            Start = place ?? new Place();
            End = place ?? new Place();
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

        public Place VisualEnd { get => End > Start ? End : Start; }
        public Place VisualStart { get => End > Start ? End : Start; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException("execute");
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public enum SelectionType
    {
        Selection, SearchMatch, WordLight
    }

    public enum Token
    {
        Normal, Environment, Command, Primitive, Definition, Comment, Dimension, Text, Reference, Key, Value, Number, Bracket, Style, Array, Symbol,
        Math
    }

    public static class Languages
    {
        public static Dictionary<Token, string> ConTeXt = new() {
            { Token.Math, @"\$.*?\$" },
            { Token.Key, @"(\w+?\s*?)(=)" },
            { Token.Symbol, @"[:=,.!?&+\-*\/\^~#;]" },
            { Token.Command, @"\\.+?\b" },
            { Token.Style, @"\\(tf|bf|it|sl|bi|bs|sc)(x|xx|[a-e])?\b|(\\tt|\\ss|\\rm)\b" },
            { Token.Array, @"\\(b|e)(T)(C|Ds?|H|N|Rs?|X|Y)\b|(\\\\|\\AR|\\DR|\\DC|\\DL|\\NI|\\NR|\\NC|\\HL|\\VL|\\FR|\\MR|\\LR|\\SR|\\TB|\\NB|\\NN|\\FL|\\ML|\\LL|\\TL|\\BL)\b" },
            { Token.Environment, @"\\(start|stop).+?\b" },
            { Token.Reference, @"\b(sec|eq|tab|fig):\w+\b" },
            { Token.Comment, @"\%.*" },
            { Token.Bracket, @"(?<!\\)(\[|\]|\(|\)|\{|\})" },
        };
    }

    public enum IntelliSenseType
    {
        Command, Argument,
    }

    public class IntelliSense
    {
        public string Text { get; set; }
        public string Description { get; set; }
        public IntelliSenseType IntelliSenseType { get; set; }
        public Token Token { get; set; }
    }

    public class SyntaxError
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public SyntaxErrorType SyntaxErrorType { get; set; } = SyntaxErrorType.None;
        public int iLine { get; set; } = 0;
        public int iChar { get; set; } = 0;
    }

    public class EditAction
    {
        public SelectionRange Selection { get; set; }
        public string InvolvedText { get; set; }
        public EditActionType EditActionType { get; set; }
    }

    public enum EditActionType
    {
        Remove, Add,
    }

    public enum SyntaxErrorType
    {
        None, Error, Warning, Message
    }


    public class WidthToThickness : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string culture)
        {
            double offset = (double)value;
            return new Thickness(0,offset,0,offset);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string culture)
        {
            return 0;
        }
    }

}
