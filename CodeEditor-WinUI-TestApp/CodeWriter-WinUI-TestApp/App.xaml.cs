using Microsoft.UI.Xaml;


namespace CodeEditor_WinUI_TestApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new Window();
            m_window.Title = "CodeWriter-WinUI";
            m_window.Content = new MainPage();
            
            m_window.Activate();
        }

        private Window m_window;
    }
}
