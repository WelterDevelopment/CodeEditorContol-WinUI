
# CodeWriter-WinUI
Win2D-based code editor for WinUI 3.

At this stage, this is only a proof of concept. 
I tried to port https://github.com/PavelTorgashov/FastColoredTextBox line by line which was a huge pain so I had to stop. The codebase is just too huge and hard to read.

I took some inspiration and created my own Win2d-based control. Feel free to contibute! Would be nice to have a fast full-featured text editor for WinAppSDK / MAUI!

## Screenshot of the TestApp
Stuff that works:
- Text selection & beam placement (inputs: PointerPress & Up/Down/Left/Right keys)
- Basic text editing (char insertion, back key, delete key)
- Basic copy & paste logic
- Scrolling (vertical & horizontal)
- Text, FontSize, Theme and TabLength are two-way bindable DependencyProperties
- Proof-of-Concept syntax highlighting shown with a ConTeXt example file (only line by line Regexing for now; tokenization comes in the future)
- Basic IntelliSense logig
- Actions (right-click menu & KeyboardAccelerators)

![Screenshot 2021-08-29 003212](https://user-images.githubusercontent.com/13318246/131232558-c26f3c68-769e-4cf4-8304-fe11cf8d8489.jpg)

## ToDo
- Middle-click scrolling
- Line wrapping
- Text folding
- Text-wide instead of line-wise regexing
- IntelliSense for commands and arguments
- Find and highlight matching bracket/parenthesis/braces pairs, auto-close pairs
- Generalize the syntax highlighting and IntelliSense for more (user-definable) languages
- Minimap
- Markers for errors and warnings
- Breakpoints and breakpoint line highlighting
- Word/Keyword/Variable highlighting
- Visual-Studio-like redundant markers for errors/warnings/breakpoints/cursorposition in the VerticelScrollbar
- Multi-cursor selection and editing
