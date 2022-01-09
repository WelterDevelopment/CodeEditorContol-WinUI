using CodeEditorControl_WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeEditor_WinUI_TestApp
{
	public sealed partial class MainPage : Page
	{
		private int actioncount = 0;

		public MainPage()
		{
			this.InitializeComponent();
			VM.Text = VM.LastSavedText = File.ReadAllText(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "ExampleText.lua"));
			CW.Save();
		}

		private ViewModel VM { get; } = new ViewModel();

		private void Btn_Add_Click(object sender, RoutedEventArgs e)
		{
			actioncount++;
			var item = new MenuFlyoutItem() { XamlRoot = CW.XamlRoot, Text = $"Induced Action {actioncount}: Selection Info", Icon = new SymbolIcon(Symbol.Help) };
			item.Click += async (a, b) =>
			{
				await new ContentDialog() { XamlRoot = CW.XamlRoot, Content = "You Selected the following text:\n" + CW.SelectedText, PrimaryButtonText = "Close", DefaultButton = ContentDialogButton.Primary }.ShowAsync();
			};
			CW.Action_Add(item);
		}

		private void Btn_Load_Click(object sender, RoutedEventArgs e)
		{
			VM.Language = "ConTeXt";
			VM.Text = VM.LastSavedText = File.ReadAllText(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "ExampleText.tex"));

			Btn_Save.Visibility = Visibility.Visible;
			Btn_Load.Content = "Reload Textfile";
		}

		private void Btn_Save_Click(object sender, RoutedEventArgs e)
		{
			File.WriteAllText(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "ExampleText.tex"), VM.Text);
			VM.LastSavedText = VM.Text;
			CW.Save(); // CodeWriter.Save() does not save file contents, it only resets all (text change, warning & error) markers
			CW_TextChanged(null,null);
		}

		private void CW_ErrorOccured(object sender, ErrorEventArgs e)
		{
			Exception ex = e.GetException();
			VM.Log += $"Error {ex.StackTrace.Replace("\r", "").Replace("\n", " -> ")}: {ex.Message}\n";
			LogScroll.ScrollToVerticalOffset(LogScroll.ScrollableHeight);
		}

		private async void CW_TextChanged(object sender, PropertyChangedEventArgs e)
		{
			// Search for syntax errors and other stuff you want to inform the user about
			List<SyntaxError> errors = new();
			await Task.Run(() =>
			{
				foreach (Line line in CW.Lines)
				{
					if (line.LineText.Count(x => x == '[') != line.LineText.Count(x => x == ']'))
						errors.Add(new() { SyntaxErrorType = SyntaxErrorType.Error, iLine = line.LineNumber - 1, Title = "Unbalanced brackets" });

					if (line.LineText.Count() == 25)
						errors.Add(new() { SyntaxErrorType = SyntaxErrorType.Warning, iLine = line.LineNumber - 1, Title = "Warning", Description = "Line contains 25 characters. That's a no-no!" });
				}
			});
			CW.SyntaxErrors = errors;
		}
	}
}