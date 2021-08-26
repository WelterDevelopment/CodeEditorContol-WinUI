using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeWriter_WinUI_TestApp
{
    public sealed partial class MainPage : Page
    {
        private ViewModel VM { get; } = new ViewModel();

        public MainPage()
        {
            this.InitializeComponent();
        }

        private  void Btn_Load_Click(object sender, RoutedEventArgs e)
        {
            VM.Text = VM.LastSavedText = File.ReadAllText(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "ExampleText.tex"));
            Btn_Save.Visibility = Visibility.Visible;
            Btn_Load.Content = "Reload Textfile";
        }

        private void Btn_Save_Click(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "ExampleText.tex"),VM.Text);
            VM.LastSavedText = VM.Text;
        }

        private void CW_ErrorOccured(object sender, ErrorEventArgs e)
        {
            Exception ex = e.GetException();
            VM.Log += $"Error {ex.StackTrace.Replace("\r", "").Replace("\n"," -> ")}: {ex.Message}\n";
            LogScroll.ScrollToVerticalOffset(LogScroll.ScrollableHeight);
        }

        int actioncount = 0;
        private void Btn_Add_Click(object sender, RoutedEventArgs e)
        {
            actioncount++;
            var item = new MenuFlyoutItem() { XamlRoot = CW.XamlRoot, Text = $"Induced Action {actioncount}: Selection Info", Icon = new SymbolIcon(Symbol.Help) };
            item.Click += async (a,b) => { 
                await new ContentDialog() { XamlRoot = CW.XamlRoot, Content = "You Selected the following text:\n"+CW.SelectedText, PrimaryButtonText = "Close", DefaultButton = ContentDialogButton.Primary }.ShowAsync(); 
            };
            CW.Action_Add(item);
        }
    }
}
