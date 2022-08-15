using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using WindowId = Microsoft.UI.WindowId;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CodeEditor_WinUI_TestApp
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainWindow : SystemBackdropWindow
	{
		public MainWindow()
		{
			this.InitializeComponent();
			IsCustomizationSupported = AppWindowTitleBar.IsCustomizationSupported();
			AW = GetAppWindowForCurrentWindow();
			AW.Title = "CodeEditorControl TestApp";

			if (IsCustomizationSupported)
			{
				AW.TitleBar.ExtendsContentIntoTitleBar = true;
				CustomDragRegion.Height = 32;
			}
			else
			{
				CustomDragRegion.BackgroundTransition = null;
				CustomDragRegion.Background = null;
				ExtendsContentIntoTitleBar = true;
				CustomDragRegion.Height = 28;
				SetTitleBar(CustomDragRegion);
				Title = "ConTeXt IDE";
			}
			ResetColors();

		}
		public void ResetColors()
		{
			if (IsCustomizationSupported)
			{
				AW.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
				AW.TitleBar.ButtonBackgroundColor = Colors.Transparent;
				AW.TitleBar.ButtonHoverBackgroundColor =  Color.FromArgb(50, 125, 125, 125) ;
				AW.TitleBar.ButtonHoverForegroundColor = ActualTheme == ApplicationTheme.Light ? Colors.Black : Colors.White;
				AW.TitleBar.ButtonForegroundColor = ActualTheme == ApplicationTheme.Light ? Colors.Black : Colors.White;
				AW.TitleBar.ButtonInactiveForegroundColor = ActualTheme == ApplicationTheme.Light ? Color.FromArgb(255, 50, 50, 50) : Color.FromArgb(255, 200, 200, 200);
			}
			else
			{
				//Application.Current.Resources["WindowCaptionBackground"] = ...;
				//Application.Current.Resources["WindowCaptionBackgroundDisabled"] = ...;
			}
		}

		public AppWindow AW { get; set; }
		public IntPtr hWnd;
		public bool IsCustomizationSupported { get; set; } = false;
		private void RootFrame_Loaded(object sender, RoutedEventArgs e)
		{
			(sender as Frame).Navigate(typeof(MainPage),null,new EntranceNavigationTransitionInfo());
		}
		private AppWindow GetAppWindowForCurrentWindow()
		{
			hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
			WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
			return AppWindow.GetFromWindowId(myWndId);
		}
	}
}
