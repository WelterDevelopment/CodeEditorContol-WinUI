using CodeWriter_WinUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeWriter_WinUI_TestApp
{
    class ViewModel : Bindable
    {
        public string LastSavedText { get => Get("Hello\nWorld!"); set { Set(value); UnsavedChanges = value != Text; } }
        public string Text { get => Get("Hello\nWorld!"); set { Set(value); UnsavedChanges = value != LastSavedText; } }
        public int FontSize { get => Get(20); set => Set(value); }
        public bool UnsavedChanges { get => Get(false); set => Set(value); }
    }
}
