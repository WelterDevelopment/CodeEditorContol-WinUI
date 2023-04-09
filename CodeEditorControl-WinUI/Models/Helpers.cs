using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace CodeEditorControl_WinUI;

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
}
public static class Extensions
{
	public static Vector2 Center(this Rect rect)
	{
		return new Vector2((float)rect.X + (float)rect.Width / 2, (float)rect.Y + (float)rect.Height / 2);
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
