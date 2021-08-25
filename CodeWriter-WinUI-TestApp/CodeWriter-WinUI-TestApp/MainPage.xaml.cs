using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CodeWriter_WinUI_TestApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
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
    }
}
