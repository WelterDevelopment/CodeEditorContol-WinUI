using System.Collections.Generic;

namespace CodeEditorControl_WinUI;

public class IntelliSense : Suggestion
{
	public IntelliSense(string text)
	{
		Name = text;
		Token = Token.Command;
	}

	public List<Argument> ArgumentsList { get; set; } = new();
}

public class Suggestion : Bindable
{
	public string Name { get; set; }

	public Token Token { get; set; } = Token.Normal;
	public IntelliSenseType IntelliSenseType { get; set; } = IntelliSenseType.Command;
	public string Snippet { get; set; } = "";
	public string Options { get; set; } = "";
	public string Description { get; set; } = "";
}


public class Argument : Suggestion
{
	public int Number { get; set; }
	public bool IsSelected { get => Get(false); set => Set(value); }
	public bool Optional { get; set; }
	public string Delimiters { get; set; }
	public string List { get; set; }
	public List<Parameter> Parameters { get; set; }
}


public class Parameter : Suggestion
{
}

public class Constant : Parameter
{
	public string Type { get; set; }
}

public class KeyValue : Parameter
{
	public List<string> Values { get; set; } = new();
}
public class BracketPair
{
	public BracketPair()
	{
	}

	public BracketPair(Place open, Place close)
	{
		iOpen = open;
		iClose = close;
	}

	public Place iClose { get; set; } = new Place();
	public Place iOpen { get; set; } = new Place();
}

public class SyntaxError
{
	public string Description { get; set; } = "";
	public int iChar { get; set; } = 0;
	public int iLine { get; set; } = 0;
	public SyntaxErrorType SyntaxErrorType { get; set; } = SyntaxErrorType.None;
	public string Title { get; set; } = "";
}
