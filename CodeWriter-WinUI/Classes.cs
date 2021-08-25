using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        internal int wordWrapIndent = 0;
       
        public List<Char> Chars { get => Get(new List<Char>()); set => Set(value); }

        public int Count
        {
            get { return Chars.Count; }
        }

        public string ErrorText { get => Get(""); set => Set(value); }
        public Folding Folding { get => Get(new Folding()); set => Set(value); }
        public string FoldingEndMarker { get; set; }
        public string FoldingStartMarker { get; set; }
        public bool IsChanged { get; set; }
        public bool IsError { get => Get(false); set => Set(value); }
        public bool IsFoldEnd { get => Get(false); set => Set(value); }
        public bool IsFoldInner { get => Get(false); set => Set(value); }
        public bool IsFoldInnerEnd { get => Get(false); set => Set(value); }
        public bool IsFoldStart { get => Get(false); set => Set(value); }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public SolidColorBrush IsSelected { get => Get(new SolidColorBrush(Colors.Transparent)); set => Set(value); }
        public bool IsWarning { get => Get(false); set => Set(value); }
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
            }
        }

        public bool Marker { get => Get(false); set => Set(value); }
        public SolidColorBrush MarkerColor { get => Get(new SolidColorBrush(Colors.ForestGreen)); set => Set(value); }
        public int startY { get => Get(0); set => Set(value); }
        public string WarningText { get => Get(""); set => Set(value); }
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

            MatchCollection math = Regex.Matches(text, @"\$.*?\$");
            foreach (Match match in math)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].T = Token.Math;
                }
            }

            MatchCollection options = Regex.Matches(text, @"(\w+?\s*?)(=)");
            foreach (Match optionmatch in options)
            {
                for (int i = optionmatch.Groups[0].Captures[0].Index; i < optionmatch.Groups[0].Captures[0].Index + optionmatch.Groups[0].Captures[0].Length - 1; i++)
                {
                    groups[i].T = Token.Key;
                }
            }

            MatchCollection commands = Regex.Matches(text, @"\\.+?\b");
            foreach (Match match in commands)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].T = Token.Command;
                }
            }

            MatchCollection startstops = Regex.Matches(text, @"\\(start|stop).+?\b");
            foreach (Match match in startstops)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].T = Token.Environment;
                }
            }

            MatchCollection brackets = Regex.Matches(text, @"(?<!\\)\[|(?<!\\)\]");
            foreach (Match match in brackets)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].T = Token.Bracket;
                }
            }

            MatchCollection braces = Regex.Matches(text, @"(?<!\\)\{|(?<!\\)\}");
            foreach (Match match in braces)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].T = Token.Bracket;
                }
            }

            MatchCollection refs = Regex.Matches(text, @"(sec|eq|tab|fig):\w+");
            foreach (Match match in refs)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].T = Token.Reference;
                }
            }

            MatchCollection linecomment = Regex.Matches(text, @"\%.*");
            foreach (Match match in linecomment)
            {
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    groups[i].T = Token.Comment;
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

        private bool Error(string text)
        {
            bool error = false;
            error = text.Count(x => x == '[') != text.Count(x => x == ']');
            ErrorText = "Line does not contain the same number of opening and closing brackets";
            return error;
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

        private bool Warning(string text)
        {
            bool error = false;
            error = text.Count() == 6;
            WarningText = "Line contains 6 characters!";
            return error;
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

    public static class Syntax
    {
        public static Color Normal(ElementTheme theme) => theme == ElementTheme.Light ? Color.FromArgb(255,20,20,20) : Color.FromArgb(255, 220, 220, 220);
    }

    public enum Token
    {
        Normal, Environment, Command, Primitive, Definition, Comment, Dimension, Reference, Key, Value, Number, Bracket, Style, Array, Symbol,
        Math
    }
}
