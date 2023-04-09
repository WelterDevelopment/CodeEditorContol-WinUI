
namespace CodeEditorControl_WinUI;
public enum IndentGuide
{
	None, Line, Dashed
}
public enum EditActionType
{
	Delete, Paste, Add, Remove
}

public enum IntelliSenseType
{
	Command, Argument,
}

public enum LexerState
{
	Normal, Comment, String
}

public enum SelectionType
{
	Selection, SearchMatch, WordLight
}

public enum SyntaxErrorType
{
	None, Error, Warning, Message
}

public enum Token
{
	Normal, Environment, Command, Function, Keyword, Primitive, Definition, String, Comment, Dimension, Text, Reference, Key, Value, Number, Bracket, Style, Array, Symbol,
	Math, Special
}

public enum VisibleState
{
	Visible, StartOfHiddenBlock, Hidden
}