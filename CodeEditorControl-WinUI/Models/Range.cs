using System;
using System.Windows.Input;

namespace CodeEditorControl_WinUI;
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

	public static Place operator +(Place p1, int c2)
	{
		return new Place(p1.iChar + c2, p1.iLine);
	}
	public static Place operator -(Place p1, int c2)
	{
		return new Place(p1.iChar - c2, p1.iLine);
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

public class RelayCommand : ICommand
{
	private readonly Func<bool> _canExecute;
	private readonly Action _execute;

	public RelayCommand(Action execute)
					: this(execute, null)
	{
	}

	public RelayCommand(Action execute, Func<bool> canExecute)
	{
		_execute = execute ?? throw new ArgumentNullException("execute");
		_canExecute = canExecute;
	}

	public event EventHandler CanExecuteChanged;

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

public class Range : Bindable
{
	public Range(Range range)
	{
		Start = range.Start;
		End = range.End;
	}
	public Range(Place place)
	{
		Start = place ?? new Place();
		End = place ?? new Place();
	}

	public Range(Place start, Place end)
	{
		Start = start;
		End = end;
	}

	public Range()
	{
	}

	public static Range operator +(Range p1, int c2)
	{
		return new Range(p1.Start + c2, p1.End + c2);
	}
	public static Range operator -(Range p1, int c2)
	{
		return new Range(p1.Start - c2, p1.End - c2);
	}

	public Place End { get => Get(new Place()); set => Set(value); }
	public Place Start { get => Get(new Place()); set => Set(value); }

	public Place VisualEnd { get => End > Start ? new(End) : new(Start); }
	public Place VisualStart { get => End > Start ? new(Start) : new(End); }

	public override string ToString() => Start.ToString() + " -> " + End.ToString();
}

public class Folding : Bindable
{
	public string Name { get => Get<string>(null); set => Set(value); }
	public int Endline { get => Get(-1); set => Set(value); }
	public int StartLine { get => Get(-1); set => Set(value); }
}

public class HighlightRange
{
	public Place End { get; set; }
	public Place Start { get; set; }
}
