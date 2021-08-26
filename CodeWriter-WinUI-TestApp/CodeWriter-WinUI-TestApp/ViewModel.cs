using CodeWriter_WinUI;
using Microsoft.UI.Xaml;
using System;

namespace CodeWriter_WinUI_TestApp
{
    class ViewModel : Bindable
    {
        public string LastSavedText { get => Get("Hello\nWorld!"); set { Set(value); UnsavedChanges = value != Text; } }
        public string Text { get => Get("Hello\nWorld!"); set { Set(value); UnsavedChanges = value != LastSavedText; } }
        public int FontSize { get => Get(20); set => Set(value); }
        public bool UnsavedChanges { get => Get(false); set => Set(value); }

        public ElementTheme RequestedTheme { get => Get(ElementTheme.Default); set => Set(value); }
        public string Theme { get => Get("Default"); set { Set(value); RequestedTheme = (ElementTheme)Enum.Parse(typeof(ElementTheme), value); } }
        public string[] ThemeOption => Enum.GetNames<ElementTheme>();

        public string Log { get => Get(""); set => Set(value); }
    }
}
