using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CodeEditorControl_WinUI;
public class WidthToThickness : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string culture)
	{
		double offset = (double)value;
		return new Thickness(0, offset, 0, offset);
	}

	public object ConvertBack(object value, Type targetType, object parameter, string culture)
	{
		return 0;
	}
}
public class Multiply : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string culture)
	{
		double input = double.Parse(value.ToString(), CultureInfo.InvariantCulture);
		double factor = double.Parse(parameter.ToString(), CultureInfo.InvariantCulture);
		return Math.Max(Math.Min(input * factor, 32), 12);
	}

	public object ConvertBack(object value, Type targetType, object parameter, string culture)
	{
		return 0;
	}
}
public class TokenToColor : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string culture)
	{
		if (value is Token token)
			return EditorOptions.TokenColors[token];
		else return EditorOptions.TokenColors[Token.Normal];
	}

	public object ConvertBack(object value, Type targetType, object parameter, string culture)
	{
		return 0;
	}
}
public class FocusToVisibility : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string culture)
	{
		FocusState state = (FocusState)value;
		return state != FocusState.Unfocused ? Visibility.Visible : Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string culture)
	{
		return 0;
	}
}
public class ArgumentsToString : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string culture)
	{
		string argstring = "";
		List<Argument> list = (List<Argument>)value;
		foreach (var item in list)
		{
			string delstart = "";
			string delend = "";
			switch (item.Delimiters)
			{
				case "parentheses ": delstart = "("; delend = ")"; break;
				case "braces": delstart = "{"; delend = "}"; break;
				case "anglebrackets": delstart = "<"; delend = ">"; break;
				case "none": delstart = ""; delend = ""; break;
				case "brackets": delstart = "["; delend = "]"; break;
				default: delstart = "["; delend = "]"; break;
			}
			argstring += " " + delstart + "..." + delend;
		}
		return argstring;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string culture)
	{
		return null;
	}
}

public class SuggestionTemplateSelector : DataTemplateSelector
{
	public DataTemplate IntelliSenseTemplate { get; set; }
	public DataTemplate ArgumentTemplate { get; set; }

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject dependency)
	{
		if (item is IntelliSense)
		{
			return IntelliSenseTemplate;
		}
		else if (item is Parameter)
		{
			return ArgumentTemplate;
		}
		else
			return null;
	}
}
