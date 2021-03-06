
# CodeEditorControl-WinUI
Win2D-based code editor control for WinUI 3.

At this stage, this is only a proof of concept. 
I tried to port https://github.com/PavelTorgashov/FastColoredTextBox line by line which was a huge pain so I had to stop. The codebase is just too huge and hard to read.

I took some inspiration and created my own Win2d-based control. Feel free to contibute! Would be nice to have a fast full-featured text editor for WinAppSDK / MAUI!

## Screenshot of the TestApp
Stuff that works:
- Text selection & beam placement (inputs: PointerPress & Up/Down/Left/Right keys)
- Basic text editing (char insertion, back key, delete key, enter key)
- Basic copy & paste logic
- Scrolling (vertical & horizontal)
- Text, FontSize, Theme and TabLength are two-way bindable DependencyProperties
- Proof-of-Concept syntax highlighting for ConTeXt as a static Language Definition within the Control
- Syntax highligting example for a Lua file in the TestApp
- Actions (right-click menu & KeyboardAccelerators)
- Middle-click scrolling
- Search and highlight
- Drag and drop
- Error/Warning/Message/SearchMatch markers on the line and on the vertical ScrollBar
- Recource-intensive work does not block the UI thread: Setting the Text, Pasting large chunks of Lines, Changing the Code Language

![Screenshot 2021-09-02 164150](https://user-images.githubusercontent.com/13318246/131864308-d7810b6e-9831-4848-9a5e-fa75a291d6f1.jpg)

![Screenshot 2021-09-02 163928](https://user-images.githubusercontent.com/13318246/131863972-107058f3-e835-4c2c-a66f-fb26e9c16e41.jpg)

## ToDo

- Line wrapping
- Text folding
- Text-wide instead of line-wise regexing (respectively lexer states for multiline-comment handling)
- Incremental regexing when Text source changes (Regex and draw the VisibleLines immediately, then regex the rest in blocks)
- IntelliSense for commands and arguments
- Find and highlight matching bracket/parenthesis/braces pairs, auto-close pairs
- Generalize the syntax highlighting and IntelliSense for more (user-definable) languages
- Minimap
- Breakpoints and breakpoint line highlighting
- Word/Keyword/Variable highlighting
- Multi-cursor selection and editing
