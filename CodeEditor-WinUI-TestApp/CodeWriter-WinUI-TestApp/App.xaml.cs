using Microsoft.UI.Xaml;


namespace CodeEditor_WinUI_TestApp
{
 public partial class App : Application
 {
  public App()
  {
   InitializeComponent();
  }

  public static MainWindow MainWindow { get; set; }

  protected override void OnLaunched(LaunchActivatedEventArgs args)
  {
			MainWindow = new MainWindow();
			MainWindow.Title = "CodeWriter-WinUI";

			MainWindow.Activate();
  }

 }
}
