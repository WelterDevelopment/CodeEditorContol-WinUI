using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Windows.Foundation;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CodeEditorControl_WinUI
{
    public enum EditActionType
    {
        Remove, Add,
    }

    public enum IntelliSenseType
    {
        Command, Argument,
    }

    public enum LexerState
    {
        Normal, Comment, String
    }

    public enum SelectionType
    {
        Selection, SearchMatch, WordLight
    }

    public enum SyntaxErrorType
    {
        None, Error, Warning, Message
    }

    public enum Token
    {
        Normal, Environment, Command, Function, Keyword, Primitive, Definition, String, Comment, Dimension, Text, Reference, Key, Value, Number, Bracket, Style, Array, Symbol,
        Math, Special
    }

    public enum VisibleState : byte
    {
        Visible, StartOfHiddenBlock, Hidden
    }

    public static class Extensions
    {
        public static Vector2 Center(this Rect rect)
        {
            return new Vector2((float)rect.X + (float)rect.Width / 2, (float)rect.Y + (float)rect.Height / 2);
        }

        public static Color ChangeColorBrightness(this Color color, float correctionFactor)
        {
            float red = color.R;
            float green = color.G;
            float blue = color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }

        public static Color InvertColorBrightness(this Color color)
        {
            // ToDo: Come up with some fancy way of producing perfect colors for the light theme
            float red = color.R;
            float green = color.G;
            float blue = color.B;

            float lumi = (0.33f * red) + (0.33f * green) + (0.33f * blue);

            red = 255 - lumi + 0.6f * (red - lumi);
            green = 255 - lumi + 0.35f * (green - lumi);
            blue = 255 - lumi + 0.4f * (blue - lumi);

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }

        public static System.Drawing.Point ToDrawingPoint(this Windows.Foundation.Point point)
        {
            return new System.Drawing.Point((int)point.X, (int)point.Y);
        }

        public static Windows.Foundation.Point ToFoundationPoint(this System.Drawing.Point point)
        {
            return new Windows.Foundation.Point(point.X, point.Y);
        }

        public static Windows.UI.Color ToUIColor(this System.Drawing.Color color)
        {
            return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static Vector2 ToVector2(this System.Drawing.Point point)
        {
            return new Vector2((float)point.X, (float)point.Y);
        }
    }

    public static class Languages
    {
        public static Language ConTeXt = new Language("ConTeXt")
        {
            RegexTokens = new()
            {
                { Token.Math, /*language=regex*/ @"\$.*?\$" },
                { Token.Key, /*language=regex*/ @"(\w+?\s*?)(=)" },
                { Token.Symbol, /*language=regex*/ @"[:=,.!?&+\-*\/\^~#;]" },
                { Token.Command, /*language=regex*/ @"\\.+?\b" },
                { Token.Style, /*language=regex*/ @"\\(tf|bf|it|sl|bi|bs|sc)(x|xx|[a-e])?\b|(\\tt|\\ss|\\rm)\b" },
                { Token.Array, /*language=regex*/ @"\\(b|e)(T)(C|Ds?|H|N|Rs?|X|Y)\b|(\\\\|\\AR|\\DR|\\DC|\\DL|\\NI|\\NR|\\NC|\\HL|\\VL|\\FR|\\MR|\\LR|\\SR|\\TB|\\NB|\\NN|\\FL|\\ML|\\LL|\\TL|\\BL)\b" },
                { Token.Environment, /*language=regex*/ @"\\(start|stop).+?\b" },
                { Token.Reference, /*language=regex*/ @"(\b|#*?)\w+?:\#*?\w+?\b|\\ref\b" },
                { Token.Comment, /*language=regex*/ @"\%.*" },
                { Token.Bracket, /*language=regex*/ @"(?<!\\)(\[|\]|\(|\)|\{|\})" },
            },
            WordTokens = new()
            {
                { Token.Primitive, new string[] { "\\year", "\\xtokspre", "\\xtoksapp", "\\xspaceskip", "\\xleaders", "\\xdef", "\\write", "\\wordboundary", "\\widowpenalty", "\\widowpenalties", "\\wd", "\\vtop", "\\vss", "\\vsplit", "\\vskip", "\\vsize", "\\vrule", "\\vpack", "\\voffset", "\\vfuzz", "\\vfilneg", "\\vfill", "\\vfil", "\\vcenter", "\\vbox", "\\vbadness", "\\valign", "\\vadjust", "\\useimageresource", "\\useboxresource", "\\uppercase", "\\unvcopy", "\\unvbox", "\\unskip", "\\unpenalty", "\\unless", "\\unkern", "\\uniformdeviate", "\\unhcopy", "\\unhbox", "\\underline", "\\uchyph", "\\uccode", "\\tracingstats", "\\tracingscantokens", "\\tracingrestores", "\\tracingparagraphs", "\\tracingpages", "\\tracingoutput", "\\tracingonline", "\\tracingnesting", "\\tracingmacros", "\\tracinglostchars", "\\tracingifs", "\\tracinggroups", "\\tracingfonts", "\\tracingcommands", "\\tracingassigns", "\\tpack", "\\topskip", "\\topmarks", "\\topmark", "\\tolerance", "\\tokspre", "\\toksdef", "\\toksapp", "\\toks", "\\time", "\\thinmuskip", "\\thickmuskip", "\\the", "\\textstyle", "\\textfont", "\\textdirection", "\\textdir", "\\tagcode", "\\tabskip", "\\synctex", "\\suppressprimitiveerror", "\\suppressoutererror", "\\suppressmathparerror", "\\suppresslongerror", "\\suppressifcsnameerror", "\\suppressfontnotfounderror", "\\string", "\\splittopskip", "\\splitmaxdepth", "\\splitfirstmarks", "\\splitfirstmark", "\\splitdiscards", "\\splitbotmarks", "\\splitbotmark", "\\special", "\\span", "\\spaceskip", "\\spacefactor", "\\skipdef", "\\skip", "\\skewchar", "\\showtokens", "\\showthe", "\\showlists", "\\showifs", "\\showgroups", "\\showboxdepth", "\\showboxbreadth", "\\showbox", "\\show", "\\shipout", "\\shapemode", "\\sfcode", "\\setrandomseed", "\\setlanguage", "\\setfontid", "\\setbox", "\\scrollmode", "\\scriptstyle", "\\scriptspace", "\\scriptscriptstyle", "\\scriptscriptfont", "\\scriptfont", "\\scantokens", "\\scantextokens", "\\savingvdiscards", "\\savinghyphcodes", "\\savepos", "\\saveimageresource", "\\savecatcodetable", "\\saveboxresource", "\\rpcode", "\\romannumeral", "\\rightskip", "\\rightmarginkern", "\\righthyphenmin", "\\rightghost", "\\right", "\\relpenalty", "\\relax", "\\readline", "\\read", "\\randomseed", "\\raise", "\\radical", "\\quitvmode", "\\pxdimen", "\\protrusionboundary", "\\protrudechars", "\\primitive", "\\prevgraf", "\\prevdepth", "\\pretolerance", "\\prerelpenalty", "\\prehyphenchar", "\\preexhyphenchar", "\\predisplaysize", "\\predisplaypenalty", "\\predisplaygapfactor", "\\predisplaydirection", "\\prebinoppenalty", "\\posthyphenchar", "\\postexhyphenchar", "\\postdisplaypenalty", "\\penalty", "\\pdfximage", "\\pdfxformresources", "\\pdfxformname", "\\pdfxformmargin", "\\pdfxformattr", "\\pdfxform", "\\pdfvorigin", "\\pdfvariable", "\\pdfuniqueresname", "\\pdfuniformdeviate", "\\pdftrailerid", "\\pdftrailer", "\\pdftracingfonts", "\\pdfthreadmargin", "\\pdfthread", "\\pdftexversion", "\\pdftexrevision", "\\pdftexbanner", "\\pdfsuppressptexinfo", "\\pdfsuppressoptionalinfo", "\\pdfstartthread", "\\pdfstartlink", "\\pdfsetrandomseed", "\\pdfsetmatrix", "\\pdfsavepos", "\\pdfsave", "\\pdfretval", "\\pdfrestore", "\\pdfreplacefont", "\\pdfrefximage", "\\pdfrefxform", "\\pdfrefobj", "\\pdfrecompress", "\\pdfrandomseed", "\\pdfpxdimen", "\\pdfprotrudechars", "\\pdfprimitive", "\\pdfpkresolution", "\\pdfpkmode", "\\pdfpkfixeddpi", "\\pdfpagewidth", "\\pdfpagesattr", "\\pdfpageresources", "\\pdfpageref", "\\pdfpageheight", "\\pdfpagebox", "\\pdfpageattr", "\\pdfoutput", "\\pdfoutline", "\\pdfomitcidset", "\\pdfomitcharset", "\\pdfobjcompresslevel", "\\pdfobj", "\\pdfnormaldeviate", "\\pdfnoligatures", "\\pdfnames", "\\pdfminorversion", "\\pdfmapline", "\\pdfmapfile", "\\pdfmajorversion", "\\pdfliteral", "\\pdflinkmargin", "\\pdflastypos", "\\pdflastxpos", "\\pdflastximagepages", "\\pdflastximage", "\\pdflastxform", "\\pdflastobj", "\\pdflastlink", "\\pdflastlinedepth", "\\pdflastannot", "\\pdfinsertht", "\\pdfinfoomitdate", "\\pdfinfo", "\\pdfinclusionerrorlevel", "\\pdfinclusioncopyfonts", "\\pdfincludechars", "\\pdfimageresolution", "\\pdfimagehicolor", "\\pdfimagegamma", "\\pdfimageapplygamma", "\\pdfimageaddfilename", "\\pdfignoreunknownimages", "\\pdfignoreddimen", "\\pdfhorigin", "\\pdfglyphtounicode", "\\pdfgentounicode", "\\pdfgamma", "\\pdffontsize", "\\pdffontobjnum", "\\pdffontname", "\\pdffontexpand", "\\pdffontattr", "\\pdffirstlineheight", "\\pdffeedback", "\\pdfextension", "\\pdfendthread", "\\pdfendlink", "\\pdfeachlineheight", "\\pdfeachlinedepth", "\\pdfdraftmode", "\\pdfdestmargin", "\\pdfdest", "\\pdfdecimaldigits", "\\pdfcreationdate", "\\pdfcopyfont", "\\pdfcompresslevel", "\\pdfcolorstackinit", "\\pdfcolorstack", "\\pdfcatalog", "\\pdfannot", "\\pdfadjustspacing", "\\pausing", "\\patterns", "\\parskip", "\\parshapelength", "\\parshapeindent", "\\parshapedimen", "\\parshape", "\\parindent", "\\parfillskip", "\\pardirection", "\\pardir", "\\par", "\\pagewidth", "\\pagetotal", "\\pagetopoffset", "\\pagestretch", "\\pageshrink", "\\pagerightoffset", "\\pageleftoffset", "\\pageheight", "\\pagegoal", "\\pagefilstretch", "\\pagefillstretch", "\\pagefilllstretch", "\\pagediscards", "\\pagedirection", "\\pagedir", "\\pagedepth", "\\pagebottomoffset", "\\overwithdelims", "\\overline", "\\overfullrule", "\\over", "\\outputpenalty", "\\outputmode", "\\outputbox", "\\output", "\\outer", "\\or", "\\openout", "\\openin", "\\omit", "\\numexpr", "\\number", "\\nullfont", "\\nulldelimiterspace", "\\novrule", "\\nospaces", "\\normalyear", "\\normalxtokspre", "\\normalxtoksapp", "\\normalxspaceskip", "\\normalxleaders", "\\normalxdef", "\\normalwrite", "\\normalwordboundary", "\\normalwidowpenalty", "\\normalwidowpenalties", "\\normalwd", "\\normalvtop", "\\normalvss", "\\normalvsplit", "\\normalvskip", "\\normalvsize", "\\normalvrule", "\\normalvpack", "\\normalvoffset", "\\normalvfuzz", "\\normalvfilneg", "\\normalvfill", "\\normalvfil", "\\normalvcenter", "\\normalvbox", "\\normalvbadness", "\\normalvalign", "\\normalvadjust", "\\normaluseimageresource", "\\normaluseboxresource", "\\normaluppercase", "\\normalunvcopy", "\\normalunvbox", "\\normalunskip", "\\normalunpenalty", "\\normalunless", "\\normalunkern", "\\normaluniformdeviate", "\\normalunhcopy", "\\normalunhbox", "\\normalunexpanded", "\\normalunderline", "\\normaluchyph", "\\normaluccode", "\\normaltracingstats", "\\normaltracingscantokens", "\\normaltracingrestores", "\\normaltracingparagraphs", "\\normaltracingpages", "\\normaltracingoutput", "\\normaltracingonline", "\\normaltracingnesting", "\\normaltracingmacros", "\\normaltracinglostchars", "\\normaltracingifs", "\\normaltracinggroups", "\\normaltracingfonts", "\\normaltracingcommands", "\\normaltracingassigns", "\\normaltpack", "\\normaltopskip", "\\normaltopmarks", "\\normaltopmark", "\\normaltolerance", "\\normaltokspre", "\\normaltoksdef", "\\normaltoksapp", "\\normaltoks", "\\normaltime", "\\normalthinmuskip", "\\normalthickmuskip", "\\normalthe", "\\normaltextstyle", "\\normaltextfont", "\\normaltextdirection", "\\normaltextdir", "\\normaltagcode", "\\normaltabskip", "\\normalsynctex", "\\normalsuppressprimitiveerror", "\\normalsuppressoutererror", "\\normalsuppressmathparerror", "\\normalsuppresslongerror", "\\normalsuppressifcsnameerror", "\\normalsuppressfontnotfounderror", "\\normalstring", "\\normalsplittopskip", "\\normalsplitmaxdepth", "\\normalsplitfirstmarks", "\\normalsplitfirstmark", "\\normalsplitdiscards", "\\normalsplitbotmarks", "\\normalsplitbotmark", "\\normalspecial", "\\normalspan", "\\normalspaceskip", "\\normalspacefactor", "\\normalskipdef", "\\normalskip", "\\normalskewchar", "\\normalshowtokens", "\\normalshowthe", "\\normalshowlists", "\\normalshowifs", "\\normalshowgroups", "\\normalshowboxdepth", "\\normalshowboxbreadth", "\\normalshowbox", "\\normalshow", "\\normalshipout", "\\normalshapemode", "\\normalsfcode", "\\normalsetrandomseed", "\\normalsetlanguage", "\\normalsetfontid", "\\normalsetbox", "\\normalscrollmode", "\\normalscriptstyle", "\\normalscriptspace", "\\normalscriptscriptstyle", "\\normalscriptscriptfont", "\\normalscriptfont", "\\normalscantokens", "\\normalscantextokens", "\\normalsavingvdiscards", "\\normalsavinghyphcodes", "\\normalsavepos", "\\normalsaveimageresource", "\\normalsavecatcodetable", "\\normalsaveboxresource", "\\normalrpcode", "\\normalromannumeral", "\\normalrightskip", "\\normalrightmarginkern", "\\normalrighthyphenmin", "\\normalrightghost", "\\normalright", "\\normalrelpenalty", "\\normalrelax", "\\normalreadline", "\\normalread", "\\normalrandomseed", "\\normalraise", "\\normalradical", "\\normalquitvmode", "\\normalpxdimen", "\\normalprotrusionboundary", "\\normalprotrudechars", "\\normalprotected", "\\normalprimitive", "\\normalprevgraf", "\\normalprevdepth", "\\normalpretolerance", "\\normalprerelpenalty", "\\normalprehyphenchar", "\\normalpreexhyphenchar", "\\normalpredisplaysize", "\\normalpredisplaypenalty", "\\normalpredisplaygapfactor", "\\normalpredisplaydirection", "\\normalprebinoppenalty", "\\normalposthyphenchar", "\\normalpostexhyphenchar", "\\normalpostdisplaypenalty", "\\normalpenalty", "\\normalpdfximage", "\\normalpdfxformresources", "\\normalpdfxformname", "\\normalpdfxformmargin", "\\normalpdfxformattr", "\\normalpdfxform", "\\normalpdfvorigin", "\\normalpdfvariable", "\\normalpdfuniqueresname", "\\normalpdfuniformdeviate", "\\normalpdftrailerid", "\\normalpdftrailer", "\\normalpdftracingfonts", "\\normalpdfthreadmargin", "\\normalpdfthread", "\\normalpdftexversion", "\\normalpdftexrevision", "\\normalpdftexbanner", "\\normalpdfsuppressptexinfo", "\\normalpdfsuppressoptionalinfo", "\\normalpdfstartthread", "\\normalpdfstartlink", "\\normalpdfsetrandomseed", "\\normalpdfsetmatrix", "\\normalpdfsavepos", "\\normalpdfsave", "\\normalpdfretval", "\\normalpdfrestore", "\\normalpdfreplacefont", "\\normalpdfrefximage", "\\normalpdfrefxform", "\\normalpdfrefobj", "\\normalpdfrecompress", "\\normalpdfrandomseed", "\\normalpdfpxdimen", "\\normalpdfprotrudechars", "\\normalpdfprimitive", "\\normalpdfpkresolution", "\\normalpdfpkmode", "\\normalpdfpkfixeddpi", "\\normalpdfpagewidth", "\\normalpdfpagesattr", "\\normalpdfpageresources", "\\normalpdfpageref", "\\normalpdfpageheight", "\\normalpdfpagebox", "\\normalpdfpageattr", "\\normalpdfoutput", "\\normalpdfoutline", "\\normalpdfomitcidset", "\\normalpdfomitcharset", "\\normalpdfobjcompresslevel", "\\normalpdfobj", "\\normalpdfnormaldeviate", "\\normalpdfnoligatures", "\\normalpdfnames", "\\normalpdfminorversion", "\\normalpdfmapline", "\\normalpdfmapfile", "\\normalpdfmajorversion", "\\normalpdfliteral", "\\normalpdflinkmargin", "\\normalpdflastypos", "\\normalpdflastxpos", "\\normalpdflastximagepages", "\\normalpdflastximage", "\\normalpdflastxform", "\\normalpdflastobj", "\\normalpdflastlink", "\\normalpdflastlinedepth", "\\normalpdflastannot", "\\normalpdfinsertht", "\\normalpdfinfoomitdate", "\\normalpdfinfo", "\\normalpdfinclusionerrorlevel", "\\normalpdfinclusioncopyfonts", "\\normalpdfincludechars", "\\normalpdfimageresolution", "\\normalpdfimagehicolor", "\\normalpdfimagegamma", "\\normalpdfimageapplygamma", "\\normalpdfimageaddfilename", "\\normalpdfignoreunknownimages", "\\normalpdfignoreddimen", "\\normalpdfhorigin", "\\normalpdfglyphtounicode", "\\normalpdfgentounicode", "\\normalpdfgamma", "\\normalpdffontsize", "\\normalpdffontobjnum", "\\normalpdffontname", "\\normalpdffontexpand", "\\normalpdffontattr", "\\normalpdffirstlineheight", "\\normalpdffeedback", "\\normalpdfextension", "\\normalpdfendthread", "\\normalpdfendlink", "\\normalpdfeachlineheight", "\\normalpdfeachlinedepth", "\\normalpdfdraftmode", "\\normalpdfdestmargin", "\\normalpdfdest", "\\normalpdfdecimaldigits", "\\normalpdfcreationdate", "\\normalpdfcopyfont", "\\normalpdfcompresslevel", "\\normalpdfcolorstackinit", "\\normalpdfcolorstack", "\\normalpdfcatalog", "\\normalpdfannot", "\\normalpdfadjustspacing", "\\normalpausing", "\\normalpatterns", "\\normalparskip", "\\normalparshapelength", "\\normalparshapeindent", "\\normalparshapedimen", "\\normalparshape", "\\normalparindent", "\\normalparfillskip", "\\normalpardirection", "\\normalpardir", "\\normalpar", "\\normalpagewidth", "\\normalpagetotal", "\\normalpagetopoffset", "\\normalpagestretch", "\\normalpageshrink", "\\normalpagerightoffset", "\\normalpageleftoffset", "\\normalpageheight", "\\normalpagegoal", "\\normalpagefilstretch", "\\normalpagefillstretch", "\\normalpagefilllstretch", "\\normalpagediscards", "\\normalpagedirection", "\\normalpagedir", "\\normalpagedepth", "\\normalpagebottomoffset", "\\normaloverwithdelims", "\\normaloverline", "\\normaloverfullrule", "\\normalover", "\\normaloutputpenalty", "\\normaloutputmode", "\\normaloutputbox", "\\normaloutput", "\\normalouter", "\\normalor", "\\normalopenout", "\\normalopenin", "\\normalomit", "\\normalnumexpr", "\\normalnumber", "\\normalnullfont", "\\normalnulldelimiterspace", "\\normalnovrule", "\\normalnospaces", "\\normalnormaldeviate", "\\normalnonstopmode", "\\normalnonscript", "\\normalnolimits", "\\normalnoligs", "\\normalnokerns", "\\normalnoindent", "\\normalnohrule", "\\normalnoexpand", "\\normalnoboundary", "\\normalnoalign", "\\normalnewlinechar", "\\normalmutoglue", "\\normalmuskipdef", "\\normalmuskip", "\\normalmultiply", "\\normalmuexpr", "\\normalmskip", "\\normalmoveright", "\\normalmoveleft", "\\normalmonth", "\\normalmkern", "\\normalmiddle", "\\normalmessage", "\\normalmedmuskip", "\\normalmeaning", "\\normalmaxdepth", "\\normalmaxdeadcycles", "\\normalmathsurroundskip", "\\normalmathsurroundmode", "\\normalmathsurround", "\\normalmathstyle", "\\normalmathscriptsmode", "\\normalmathscriptcharmode", "\\normalmathscriptboxmode", "\\normalmathrulethicknessmode", "\\normalmathrulesmode", "\\normalmathrulesfam", "\\normalmathrel", "\\normalmathpunct", "\\normalmathpenaltiesmode", "\\normalmathord", "\\normalmathoption", "\\normalmathopen", "\\normalmathop", "\\normalmathnolimitsmode", "\\normalmathitalicsmode", "\\normalmathinner", "\\normalmathflattenmode", "\\normalmatheqnogapstep", "\\normalmathdisplayskipmode", "\\normalmathdirection", "\\normalmathdir", "\\normalmathdelimitersmode", "\\normalmathcode", "\\normalmathclose", "\\normalmathchoice", "\\normalmathchardef", "\\normalmathchar", "\\normalmathbin", "\\normalmathaccent", "\\normalmarks", "\\normalmark", "\\normalmag", "\\normalluatexversion", "\\normalluatexrevision", "\\normalluatexbanner", "\\normalluafunctioncall", "\\normalluafunction", "\\normalluaescapestring", "\\normalluadef", "\\normalluacopyinputnodes", "\\normalluabytecodecall", "\\normalluabytecode", "\\normallpcode", "\\normallowercase", "\\normallower", "\\normallooseness", "\\normallong", "\\normallocalrightbox", "\\normallocalleftbox", "\\normallocalinterlinepenalty", "\\normallocalbrokenpenalty", "\\normallinepenalty", "\\normallinedirection", "\\normallinedir", "\\normallimits", "\\normalletterspacefont", "\\normalletcharcode", "\\normallet", "\\normalleqno", "\\normalleftskip", "\\normalleftmarginkern", "\\normallefthyphenmin", "\\normalleftghost", "\\normalleft", "\\normalleaders", "\\normallccode", "\\normallateluafunction", "\\normallatelua", "\\normallastypos", "\\normallastxpos", "\\normallastskip", "\\normallastsavedimageresourcepages", "\\normallastsavedimageresourceindex", "\\normallastsavedboxresourceindex", "\\normallastpenalty", "\\normallastnodetype", "\\normallastnamedcs", "\\normallastlinefit", "\\normallastkern", "\\normallastbox", "\\normallanguage", "\\normalkern", "\\normaljobname", "\\normalinterlinepenalty", "\\normalinterlinepenalties", "\\normalinteractionmode", "\\normalinsertpenalties", "\\normalinsertht", "\\normalinsert", "\\normalinputlineno", "\\normalinput", "\\normalinitcatcodetable", "\\normalindent", "\\normalimmediateassignment", "\\normalimmediateassigned", "\\normalimmediate", "\\normalignorespaces", "\\normalignoreligaturesinfont", "\\normalifx", "\\normalifvoid", "\\normalifvmode", "\\normalifvbox", "\\normaliftrue", "\\normalifprimitive", "\\normalifpdfprimitive", "\\normalifpdfabsnum", "\\normalifpdfabsdim", "\\normalifodd", "\\normalifnum", "\\normalifmmode", "\\normalifinner", "\\normalifincsname", "\\normalifhmode", "\\normalifhbox", "\\normaliffontchar", "\\normaliffalse", "\\normalifeof", "\\normalifdim", "\\normalifdefined", "\\normalifcsname", "\\normalifcondition", "\\normalifcat", "\\normalifcase", "\\normalifabsnum", "\\normalifabsdim", "\\normalif", "\\normalhyphenpenaltymode", "\\normalhyphenpenalty", "\\normalhyphenchar", "\\normalhyphenationmin", "\\normalhyphenationbounds", "\\normalhyphenation", "\\normalht", "\\normalhss", "\\normalhskip", "\\normalhsize", "\\normalhrule", "\\normalhpack", "\\normalholdinginserts", "\\normalhoffset", "\\normalhjcode", "\\normalhfuzz", "\\normalhfilneg", "\\normalhfill", "\\normalhfil", "\\normalhbox", "\\normalhbadness", "\\normalhangindent", "\\normalhangafter", "\\normalhalign", "\\normalgtokspre", "\\normalgtoksapp", "\\normalgluetomu", "\\normalgluestretchorder", "\\normalgluestretch", "\\normalglueshrinkorder", "\\normalglueshrink", "\\normalglueexpr", "\\normalglobaldefs", "\\normalglobal", "\\normalglet", "\\normalgleaders", "\\normalgdef", "\\normalfuturelet", "\\normalfutureexpandis", "\\normalfutureexpand", "\\normalformatname", "\\normalfontname", "\\normalfontid", "\\normalfontdimen", "\\normalfontcharwd", "\\normalfontcharic", "\\normalfontcharht", "\\normalfontchardp", "\\normalfont", "\\normalfloatingpenalty", "\\normalfixupboxesmode", "\\normalfirstvalidlanguage", "\\normalfirstmarks", "\\normalfirstmark", "\\normalfinalhyphendemerits", "\\normalfi", "\\normalfam", "\\normalexplicithyphenpenalty", "\\normalexplicitdiscretionary", "\\normalexpandglyphsinfont", "\\normalexpanded", "\\normalexpandafter", "\\normalexhyphenpenalty", "\\normalexhyphenchar", "\\normalexceptionpenalty", "\\normaleveryvbox", "\\normaleverypar", "\\normaleverymath", "\\normaleveryjob", "\\normaleveryhbox", "\\normaleveryeof", "\\normaleverydisplay", "\\normaleverycr", "\\normaletokspre", "\\normaletoksapp", "\\normalescapechar", "\\normalerrorstopmode", "\\normalerrorcontextlines", "\\normalerrmessage", "\\normalerrhelp", "\\normaleqno", "\\normalendlocalcontrol", "\\normalendlinechar", "\\normalendinput", "\\normalendgroup", "\\normalendcsname", "\\normalend", "\\normalemergencystretch", "\\normalelse", "\\normalefcode", "\\normaledef", "\\normaleTeXversion", "\\normaleTeXrevision", "\\normaleTeXminorversion", "\\normaleTeXVersion", "\\normaldvivariable", "\\normaldvifeedback", "\\normaldviextension", "\\normaldump", "\\normaldraftmode", "\\normaldp", "\\normaldoublehyphendemerits", "\\normaldivide", "\\normaldisplaywidth", "\\normaldisplaywidowpenalty", "\\normaldisplaywidowpenalties", "\\normaldisplaystyle", "\\normaldisplaylimits", "\\normaldisplayindent", "\\normaldiscretionary", "\\normaldirectlua", "\\normaldimexpr", "\\normaldimendef", "\\normaldimen", "\\normaldeviate", "\\normaldetokenize", "\\normaldelimitershortfall", "\\normaldelimiterfactor", "\\normaldelimiter", "\\normaldelcode", "\\normaldefaultskewchar", "\\normaldefaulthyphenchar", "\\normaldef", "\\normaldeadcycles", "\\normalday", "\\normalcurrentiftype", "\\normalcurrentiflevel", "\\normalcurrentifbranch", "\\normalcurrentgrouptype", "\\normalcurrentgrouplevel", "\\normalcsstring", "\\normalcsname", "\\normalcrcr", "\\normalcrampedtextstyle", "\\normalcrampedscriptstyle", "\\normalcrampedscriptscriptstyle", "\\normalcrampeddisplaystyle", "\\normalcr", "\\normalcountdef", "\\normalcount", "\\normalcopyfont", "\\normalcopy", "\\normalcompoundhyphenmode", "\\normalclubpenalty", "\\normalclubpenalties", "\\normalcloseout", "\\normalclosein", "\\normalclearmarks", "\\normalcleaders", "\\normalchardef", "\\normalchar", "\\normalcatcodetable", "\\normalcatcode", "\\normalbrokenpenalty", "\\normalbreakafterdirmode", "\\normalboxmaxdepth", "\\normalboxdirection", "\\normalboxdir", "\\normalbox", "\\normalboundary", "\\normalbotmarks", "\\normalbotmark", "\\normalbodydirection", "\\normalbodydir", "\\normalbinoppenalty", "\\normalbelowdisplayskip", "\\normalbelowdisplayshortskip", "\\normalbegingroup", "\\normalbegincsname", "\\normalbatchmode", "\\normalbadness", "\\normalautomatichyphenpenalty", "\\normalautomatichyphenmode", "\\normalautomaticdiscretionary", "\\normalattributedef", "\\normalattribute", "\\normalatopwithdelims", "\\normalatop", "\\normalaligntab", "\\normalalignmark", "\\normalaftergroup", "\\normalafterassignment", "\\normaladvance", "\\normaladjustspacing", "\\normaladjdemerits", "\\normalaccent", "\\normalabovewithdelims", "\\normalabovedisplayskip", "\\normalabovedisplayshortskip", "\\normalabove", "\\normalXeTeXversion", "\\normalUvextensible", "\\normalUunderdelimiter", "\\normalUsuperscript", "\\normalUsubscript", "\\normalUstopmath", "\\normalUstopdisplaymath", "\\normalUstartmath", "\\normalUstartdisplaymath", "\\normalUstack", "\\normalUskewedwithdelims", "\\normalUskewed", "\\normalUroot", "\\normalUright", "\\normalUradical", "\\normalUoverdelimiter", "\\normalUnosuperscript", "\\normalUnosubscript", "\\normalUmiddle", "\\normalUmathunderdelimitervgap", "\\normalUmathunderdelimiterbgap", "\\normalUmathunderbarvgap", "\\normalUmathunderbarrule", "\\normalUmathunderbarkern", "\\normalUmathsupsubbottommax", "\\normalUmathsupshiftup", "\\normalUmathsupshiftdrop", "\\normalUmathsupbottommin", "\\normalUmathsubtopmax", "\\normalUmathsubsupvgap", "\\normalUmathsubsupshiftdown", "\\normalUmathsubshiftdrop", "\\normalUmathsubshiftdown", "\\normalUmathstackvgap", "\\normalUmathstacknumup", "\\normalUmathstackdenomdown", "\\normalUmathspaceafterscript", "\\normalUmathskewedfractionvgap", "\\normalUmathskewedfractionhgap", "\\normalUmathrelrelspacing", "\\normalUmathrelpunctspacing", "\\normalUmathrelordspacing", "\\normalUmathrelopspacing", "\\normalUmathrelopenspacing", "\\normalUmathrelinnerspacing", "\\normalUmathrelclosespacing", "\\normalUmathrelbinspacing", "\\normalUmathradicalvgap", "\\normalUmathradicalrule", "\\normalUmathradicalkern", "\\normalUmathradicaldegreeraise", "\\normalUmathradicaldegreebefore", "\\normalUmathradicaldegreeafter", "\\normalUmathquad", "\\normalUmathpunctrelspacing", "\\normalUmathpunctpunctspacing", "\\normalUmathpunctordspacing", "\\normalUmathpunctopspacing", "\\normalUmathpunctopenspacing", "\\normalUmathpunctinnerspacing", "\\normalUmathpunctclosespacing", "\\normalUmathpunctbinspacing", "\\normalUmathoverdelimitervgap", "\\normalUmathoverdelimiterbgap", "\\normalUmathoverbarvgap", "\\normalUmathoverbarrule", "\\normalUmathoverbarkern", "\\normalUmathordrelspacing", "\\normalUmathordpunctspacing", "\\normalUmathordordspacing", "\\normalUmathordopspacing", "\\normalUmathordopenspacing", "\\normalUmathordinnerspacing", "\\normalUmathordclosespacing", "\\normalUmathordbinspacing", "\\normalUmathoprelspacing", "\\normalUmathoppunctspacing", "\\normalUmathopordspacing", "\\normalUmathopopspacing", "\\normalUmathopopenspacing", "\\normalUmathopinnerspacing", "\\normalUmathoperatorsize", "\\normalUmathopenrelspacing", "\\normalUmathopenpunctspacing", "\\normalUmathopenordspacing", "\\normalUmathopenopspacing", "\\normalUmathopenopenspacing", "\\normalUmathopeninnerspacing", "\\normalUmathopenclosespacing", "\\normalUmathopenbinspacing", "\\normalUmathopclosespacing", "\\normalUmathopbinspacing", "\\normalUmathnolimitsupfactor", "\\normalUmathnolimitsubfactor", "\\normalUmathlimitbelowvgap", "\\normalUmathlimitbelowkern", "\\normalUmathlimitbelowbgap", "\\normalUmathlimitabovevgap", "\\normalUmathlimitabovekern", "\\normalUmathlimitabovebgap", "\\normalUmathinnerrelspacing", "\\normalUmathinnerpunctspacing", "\\normalUmathinnerordspacing", "\\normalUmathinneropspacing", "\\normalUmathinneropenspacing", "\\normalUmathinnerinnerspacing", "\\normalUmathinnerclosespacing", "\\normalUmathinnerbinspacing", "\\normalUmathfractionrule", "\\normalUmathfractionnumvgap", "\\normalUmathfractionnumup", "\\normalUmathfractiondenomvgap", "\\normalUmathfractiondenomdown", "\\normalUmathfractiondelsize", "\\normalUmathconnectoroverlapmin", "\\normalUmathcodenum", "\\normalUmathcode", "\\normalUmathcloserelspacing", "\\normalUmathclosepunctspacing", "\\normalUmathcloseordspacing", "\\normalUmathcloseopspacing", "\\normalUmathcloseopenspacing", "\\normalUmathcloseinnerspacing", "\\normalUmathcloseclosespacing", "\\normalUmathclosebinspacing", "\\normalUmathcharslot", "\\normalUmathcharnumdef", "\\normalUmathcharnum", "\\normalUmathcharfam", "\\normalUmathchardef", "\\normalUmathcharclass", "\\normalUmathchar", "\\normalUmathbinrelspacing", "\\normalUmathbinpunctspacing", "\\normalUmathbinordspacing", "\\normalUmathbinopspacing", "\\normalUmathbinopenspacing", "\\normalUmathbininnerspacing", "\\normalUmathbinclosespacing", "\\normalUmathbinbinspacing", "\\normalUmathaxis", "\\normalUmathaccent", "\\normalUleft", "\\normalUhextensible", "\\normalUdelimiterunder", "\\normalUdelimiterover", "\\normalUdelimiter", "\\normalUdelcodenum", "\\normalUdelcode", "\\normalUchar", "\\normalOmegaversion", "\\normalOmegarevision", "\\normalOmegaminorversion", "\\normalAlephversion", "\\normalAlephrevision", "\\normalAlephminorversion", "\\normal", "\\nonstopmode", "\\nonscript", "\\nolimits", "\\noligs", "\\nokerns", "\\noindent", "\\nohrule", "\\noexpand", "\\noboundary", "\\noalign", "\\newlinechar", "\\mutoglue", "\\muskipdef", "\\muskip", "\\multiply", "\\muexpr", "\\mskip", "\\moveright", "\\moveleft", "\\month", "\\mkern", "\\middle", "\\message", "\\medmuskip", "\\meaning", "\\maxdepth", "\\maxdeadcycles", "\\mathsurroundskip", "\\mathsurroundmode", "\\mathsurround", "\\mathstyle", "\\mathscriptsmode", "\\mathscriptcharmode", "\\mathscriptboxmode", "\\mathrulethicknessmode", "\\mathrulesmode", "\\mathrulesfam", "\\mathrel", "\\mathpunct", "\\mathpenaltiesmode", "\\mathord", "\\mathoption", "\\mathopen", "\\mathop", "\\mathnolimitsmode", "\\mathitalicsmode", "\\mathinner", "\\mathflattenmode", "\\matheqnogapstep", "\\mathdisplayskipmode", "\\mathdirection", "\\mathdir", "\\mathdelimitersmode", "\\mathcode", "\\mathclose", "\\mathchoice", "\\mathchardef", "\\mathchar", "\\mathbin", "\\mathaccent", "\\marks", "\\mark", "\\mag", "\\luatexversion", "\\luatexrevision", "\\luatexbanner", "\\luafunctioncall", "\\luafunction", "\\luaescapestring", "\\luadef", "\\luacopyinputnodes", "\\luabytecodecall", "\\luabytecode", "\\lpcode", "\\lowercase", "\\lower", "\\looseness", "\\long", "\\localrightbox", "\\localleftbox", "\\localinterlinepenalty", "\\localbrokenpenalty", "\\lineskiplimit", "\\lineskip", "\\linepenalty", "\\linedirection", "\\linedir", "\\limits", "\\letterspacefont", "\\letcharcode", "\\let", "\\leqno", "\\leftskip", "\\leftmarginkern", "\\lefthyphenmin", "\\leftghost", "\\left", "\\leaders", "\\lccode", "\\lateluafunction", "\\latelua", "\\lastypos", "\\lastxpos", "\\lastskip", "\\lastsavedimageresourcepages", "\\lastsavedimageresourceindex", "\\lastsavedboxresourceindex", "\\lastpenalty", "\\lastnodetype", "\\lastnamedcs", "\\lastlinefit", "\\lastkern", "\\lastbox", "\\language", "\\kern", "\\jobname", "\\interlinepenalty", "\\interlinepenalties", "\\interactionmode", "\\insertpenalties", "\\insertht", "\\insert", "\\inputlineno", "\\input", "\\initcatcodetable", "\\indent", "\\immediateassignment", "\\immediateassigned", "\\immediate", "\\ignorespaces", "\\ignoreligaturesinfont", "\\ifx", "\\ifvoid", "\\ifvmode", "\\ifvbox", "\\iftrue", "\\ifprimitive", "\\ifpdfprimitive", "\\ifpdfabsnum", "\\ifpdfabsdim", "\\ifodd", "\\ifnum", "\\ifmmode", "\\ifinner", "\\ifincsname", "\\ifhmode", "\\ifhbox", "\\iffontchar", "\\iffalse", "\\ifeof", "\\ifdim", "\\ifdefined", "\\ifcsname", "\\ifcondition", "\\ifcat", "\\ifcase", "\\ifabsnum", "\\ifabsdim", "\\if", "\\hyphenpenaltymode", "\\hyphenpenalty", "\\hyphenchar", "\\hyphenationmin", "\\hyphenationbounds", "\\hyphenation", "\\ht", "\\hss", "\\hskip", "\\hsize", "\\hrule", "\\hpack", "\\holdinginserts", "\\hoffset", "\\hjcode", "\\hfuzz", "\\hfilneg", "\\hfill", "\\hfil", "\\hbox", "\\hbadness", "\\hangindent", "\\hangafter", "\\halign", "\\gtokspre", "\\gtoksapp", "\\gluetomu", "\\gluestretchorder", "\\gluestretch", "\\glueshrinkorder", "\\glueshrink", "\\glueexpr", "\\globaldefs", "\\global", "\\gleaders", "\\gdef", "\\futurelet", "\\futureexpandis", "\\futureexpand", "\\formatname", "\\fontname", "\\fontid", "\\fontdimen", "\\fontcharwd", "\\fontcharic", "\\fontcharht", "\\fontchardp", "\\font", "\\floatingpenalty", "\\fixupboxesmode", "\\firstvalidlanguage", "\\firstmarks", "\\firstmark", "\\finalhyphendemerits", "\\fi", "\\fam", "\\explicithyphenpenalty", "\\explicitdiscretionary", "\\expandglyphsinfont", "\\expandafter", "\\exhyphenpenalty", "\\exhyphenchar", "\\exceptionpenalty", "\\everyvbox", "\\everypar", "\\everymath", "\\everyjob", "\\everyhbox", "\\everyeof", "\\everydisplay", "\\everycr", "\\etokspre", "\\etoksapp", "\\escapechar", "\\errorstopmode", "\\errorcontextlines", "\\errmessage", "\\errhelp", "\\eqno", "\\endlocalcontrol", "\\endlinechar", "\\endinput", "\\endgroup", "\\endcsname", "\\end", "\\emergencystretch", "\\else", "\\efcode", "\\edef", "\\eTeXversion", "\\eTeXrevision", "\\eTeXminorversion", "\\eTeXVersion", "\\dvivariable", "\\dvifeedback", "\\dviextension", "\\dump", "\\draftmode", "\\dp", "\\doublehyphendemerits", "\\divide", "\\displaywidth", "\\displaywidowpenalty", "\\displaywidowpenalties", "\\displaystyle", "\\displaylimits", "\\displayindent", "\\discretionary", "\\directlua", "\\dimexpr", "\\dimendef", "\\dimen", "\\detokenize", "\\delimitershortfall", "\\delimiterfactor", "\\delimiter", "\\delcode", "\\defaultskewchar", "\\defaulthyphenchar", "\\def", "\\deadcycles", "\\day", "\\currentiftype", "\\currentiflevel", "\\currentifbranch", "\\currentgrouptype", "\\currentgrouplevel", "\\csstring", "\\csname", "\\crcr", "\\crampedtextstyle", "\\crampedscriptstyle", "\\crampedscriptscriptstyle", "\\crampeddisplaystyle", "\\cr", "\\countdef", "\\count", "\\copyfont", "\\copy", "\\compoundhyphenmode", "\\clubpenalty", "\\clubpenalties", "\\closeout", "\\closein", "\\clearmarks", "\\cleaders", "\\chardef", "\\char", "\\catcodetable", "\\catcode", "\\brokenpenalty", "\\breakafterdirmode", "\\boxmaxdepth", "\\boxdirection", "\\boxdir", "\\box", "\\boundary", "\\botmarks", "\\botmark", "\\bodydirection", "\\bodydir", "\\binoppenalty", "\\belowdisplayskip", "\\belowdisplayshortskip", "\\begingroup", "\\begincsname", "\\batchmode", "\\baselineskip", "\\badness", "\\automatichyphenpenalty", "\\automatichyphenmode", "\\automaticdiscretionary", "\\attributedef", "\\attribute", "\\atopwithdelims", "\\atop", "\\aligntab", "\\alignmark", "\\aftergroup", "\\afterassignment", "\\advance", "\\adjustspacing", "\\adjdemerits", "\\accent", "\\abovewithdelims", "\\abovedisplayskip", "\\abovedisplayshortskip", "\\above", "\\XeTeXversion", "\\Uvextensible", "\\Uunderdelimiter", "\\Usuperscript", "\\Usubscript", "\\Ustopmath", "\\Ustopdisplaymath", "\\Ustartmath", "\\Ustartdisplaymath", "\\Ustack", "\\Uskewedwithdelims", "\\Uskewed", "\\Uroot", "\\Uright", "\\Uradical", "\\Uoverdelimiter", "\\Unosuperscript", "\\Unosubscript", "\\Umiddle", "\\Umathunderdelimitervgap", "\\Umathunderdelimiterbgap", "\\Umathunderbarvgap", "\\Umathunderbarrule", "\\Umathunderbarkern", "\\Umathsupsubbottommax", "\\Umathsupshiftup", "\\Umathsupshiftdrop", "\\Umathsupbottommin", "\\Umathsubtopmax", "\\Umathsubsupvgap", "\\Umathsubsupshiftdown", "\\Umathsubshiftdrop", "\\Umathsubshiftdown", "\\Umathstackvgap", "\\Umathstacknumup", "\\Umathstackdenomdown", "\\Umathspaceafterscript", "\\Umathskewedfractionvgap", "\\Umathskewedfractionhgap", "\\Umathrelrelspacing", "\\Umathrelpunctspacing", "\\Umathrelordspacing", "\\Umathrelopspacing", "\\Umathrelopenspacing", "\\Umathrelinnerspacing", "\\Umathrelclosespacing", "\\Umathrelbinspacing", "\\Umathradicalvgap", "\\Umathradicalrule", "\\Umathradicalkern", "\\Umathradicaldegreeraise", "\\Umathradicaldegreebefore", "\\Umathradicaldegreeafter", "\\Umathquad", "\\Umathpunctrelspacing", "\\Umathpunctpunctspacing", "\\Umathpunctordspacing", "\\Umathpunctopspacing", "\\Umathpunctopenspacing", "\\Umathpunctinnerspacing", "\\Umathpunctclosespacing", "\\Umathpunctbinspacing", "\\Umathoverdelimitervgap", "\\Umathoverdelimiterbgap", "\\Umathoverbarvgap", "\\Umathoverbarrule", "\\Umathoverbarkern", "\\Umathordrelspacing", "\\Umathordpunctspacing", "\\Umathordordspacing", "\\Umathordopspacing", "\\Umathordopenspacing", "\\Umathordinnerspacing", "\\Umathordclosespacing", "\\Umathordbinspacing", "\\Umathoprelspacing", "\\Umathoppunctspacing", "\\Umathopordspacing", "\\Umathopopspacing", "\\Umathopopenspacing", "\\Umathopinnerspacing", "\\Umathoperatorsize", "\\Umathopenrelspacing", "\\Umathopenpunctspacing", "\\Umathopenordspacing", "\\Umathopenopspacing", "\\Umathopenopenspacing", "\\Umathopeninnerspacing", "\\Umathopenclosespacing", "\\Umathopenbinspacing", "\\Umathopclosespacing", "\\Umathopbinspacing", "\\Umathnolimitsupfactor", "\\Umathnolimitsubfactor", "\\Umathlimitbelowvgap", "\\Umathlimitbelowkern", "\\Umathlimitbelowbgap", "\\Umathlimitabovevgap", "\\Umathlimitabovekern", "\\Umathlimitabovebgap", "\\Umathinnerrelspacing", "\\Umathinnerpunctspacing", "\\Umathinnerordspacing", "\\Umathinneropspacing", "\\Umathinneropenspacing", "\\Umathinnerinnerspacing", "\\Umathinnerclosespacing", "\\Umathinnerbinspacing", "\\Umathfractionrule", "\\Umathfractionnumvgap", "\\Umathfractionnumup", "\\Umathfractiondenomvgap", "\\Umathfractiondenomdown", "\\Umathfractiondelsize", "\\Umathconnectoroverlapmin", "\\Umathcodenum", "\\Umathcode", "\\Umathcloserelspacing", "\\Umathclosepunctspacing", "\\Umathcloseordspacing", "\\Umathcloseopspacing", "\\Umathcloseopenspacing", "\\Umathcloseinnerspacing", "\\Umathcloseclosespacing", "\\Umathclosebinspacing", "\\Umathcharslot", "\\Umathcharnumdef", "\\Umathcharnum", "\\Umathcharfam", "\\Umathchardef", "\\Umathcharclass", "\\Umathchar", "\\Umathbinrelspacing", "\\Umathbinpunctspacing", "\\Umathbinordspacing", "\\Umathbinopspacing", "\\Umathbinopenspacing", "\\Umathbininnerspacing", "\\Umathbinclosespacing", "\\Umathbinbinspacing", "\\Umathaxis", "\\Umathaccent", "\\Uleft", "\\Uhextensible", "\\Udelimiterunder", "\\Udelimiterover", "\\Udelimiter", "\\Udelcodenum", "\\Udelcode", "\\Uchar", "\\Omegaversion", "\\Omegarevision", "\\Omegaminorversion", "\\Alephversion", "\\Alephrevision", "\\Alephminorversion" } },
            },
            FoldingPairs = new()
            {
                new SyntaxFolding() { RegexStart = /*language=regex*/ @"\\(start).+?\b", RegexEnd = /*language=regex*/ @"\\(stop).+?\b" },
            },
            CommandTriggerCharacters = new[] { '\\' },
        };
    }

    public static class EditorOptions
    {
        public static Dictionary<Token, Color> TokenColors = new()
        {
            { Token.Normal, Color.FromArgb(255, 220, 220, 220) },
            { Token.Command, Color.FromArgb(255, 50, 130, 210) },
            { Token.Function, Color.FromArgb(255, 200, 120, 220) },
            { Token.Keyword, Color.FromArgb(255, 50, 130, 210) },
            { Token.Environment, Color.FromArgb(255, 50, 190, 150) },
            { Token.Comment, Color.FromArgb(255, 40, 190, 90) },
            { Token.Key, Color.FromArgb(255, 150, 120, 200) },
            { Token.Bracket, Color.FromArgb(255, 100, 140, 220) },
            { Token.Reference, Color.FromArgb(255, 180, 140, 40) },
            { Token.Math, Color.FromArgb(255, 220, 160, 60) },
            { Token.Symbol, Color.FromArgb(255, 140, 200, 240) },
            { Token.Style, Color.FromArgb(255, 220, 130, 100) },
            { Token.String, Color.FromArgb(255, 220, 130, 100) },
            { Token.Special, Color.FromArgb(255, 50, 190, 150) },
            { Token.Number, Color.FromArgb(255, 180, 220, 180) },
            { Token.Array, Color.FromArgb(255, 200, 100, 80) },
            { Token.Primitive, Color.FromArgb(255, 230, 120, 100) },
        };
    }

    public class Bindable : INotifyPropertyChanged
    {
        private Dictionary<string, object> _properties = new Dictionary<string, object>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected T Get<T>(T defaultVal = default, [CallerMemberName] string name = null)
        {
            if (!_properties.TryGetValue(name, out object value))
            {
                value = _properties[name] = defaultVal;
            }
            return (T)value;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void Set<T>(T value, [CallerMemberName] string name = null)
        {
            if (Equals(value, Get<T>(value, name)))
                return;
            _properties[name] = value;
            OnPropertyChanged(name);
        }
    }

    public class BracketPair
    {
        public BracketPair()
        {
        }

        public BracketPair(Place open, Place close)
        {
            iOpen = open;
            iClose = close;
        }

        public Place iClose { get; set; } = new Place();
        public Place iOpen { get; set; } = new Place();
    }

    public class Char : CharElement
    {
        public Char(char c)
        {
            C = c;
        }
    }

    public class CharElement : Bindable
    {
        public char C { get => Get(' '); set => Set(value); }

        //public Color ForeGround { get => Get(Colors.White); set { Set(value); } }
        public Token T { get => Get(Token.Normal); set => Set(value); }
    }

    public class CharGroup : CharElement
    {
        public CharGroup(Char[] chars)
        {
            C = chars;
        }

        public new Char[] C { get => Get(new Char[] { }); set => Set(value); }
    }

    public class CodeWriterOptions : Bindable
    {
    }

    public class EditAction
    {
        public EditActionType EditActionType { get; set; }
        public string InvolvedText { get; set; }
        public Range Selection { get; set; }
    }

    public class Folding : Bindable
    {
        public int Endline { get => Get(0); set => Set(value); }
        public int StartLine { get => Get(0); set => Set(value); }
    }

    public class HighlightRange
    {
        public Place End { get; set; }
        public Place Start { get; set; }
    }

    public class IntelliSense
    {
        public IntelliSense(string text)
        {
            Text = text;
        }
        public string Description { get; set; }
        public IntelliSenseType IntelliSenseType { get; set; } = IntelliSenseType.Command;
        public string Text { get; set; }
        public string Snippet { get; set; }
        public Token Token { get; set; } = Token.Command;
    }

    public class Language
    {
        public Language(string language)
        {
            Name = language;
        }

        public List<SyntaxFolding> FoldingPairs { get; set; }
        public string Name { get; set; }

        public char[] CommandTriggerCharacters { get; set; } = new char[] { };
        public char[] OptionsTriggerCharacters { get; set; } = new char[] { };
        public Dictionary<Token, string> RegexTokens { get; set; }
        public Dictionary<Token, string[]> WordTokens { get; set; }
        public List<IntelliSense> Commands {  get; set; }
    }

    public class Line : Bindable
    {
        public VisibleState VisibleState = VisibleState.Visible;

        private string lastsavedtext = null;

        public Line(Language language = null)
        {
            if (language != null)
                Language = language;
        }

        public List<Char> Chars { get => Get(new List<Char>()); set => Set(value); }

        public int Count
        {
            get { return Chars.Count; }
        }

        public Folding Folding { get => Get(new Folding()); set => Set(value); }

        public string FoldingEndMarker { get; set; }

        public string FoldingStartMarker { get; set; }

        public int iLine { get => LineNumber - 1; }

        public int Indents
        {
            get { return LineText.Count(x => x == '\t'); }
        }

        public bool IsFoldEnd { get => Get(false); set => Set(value); }
        public bool IsFoldInner { get => Get(false); set => Set(value); }
        public bool IsFoldInnerEnd { get => Get(false); set => Set(value); }
        public bool IsFoldStart { get => Get(false); set => Set(value); }
        public bool IsUnsaved { get => Get(false); set { Set(value); if (!value) lastsavedtext = LineText; } }
        public Language Language { get => Get<Language>(); set { Set(value); Chars = FormattedText(LineText); } }
        public int LineNumber { get => Get(0); set => Set(value); }

        public string LineText
        {
            get => Get("");
            set
            {
                IsUnsaved = value != lastsavedtext;
                Set(value);

                Chars = FormattedText(value);
                IsFoldStart = FoldableStart(value);
                IsFoldInnerEnd = FoldableEnd(value);
                IsFoldInner = !IsFoldStart && !IsFoldInnerEnd;
            }
        }

        public int WordWrapStringsCount { get; internal set; }

        public Char this[int index]
        {
            get
            {
                return Chars[index];
            }
            set
            {
                Chars[index] = value;
            }
        }

        public void Add(Char item)
        {
            Chars.Add(item);
        }

        public virtual void AddRange(IEnumerable<Char> collection)
        {
            //Chars.AddRange(collection);
        }

        public void Clear()
        {
            Chars.Clear();
        }

        public bool Contains(Char item)
        {
            return Chars.Contains(item);
        }

        public void CopyTo(Char[] array, int arrayIndex)
        {
            Chars.CopyTo(array, arrayIndex);
        }

        public List<Char> FormattedText(string text)
        {
            List<Char> groups = new();

            groups = text.Select(x => new Char(x)).ToList();

            if (Language.WordTokens != null)
                foreach (var token in Language.WordTokens)
                {
                    var list = token.Value.ToList();
                    for (int i = 0; i < list.Count; i++)
                    {
                        list[i] = list[i].Replace(@"\", @"\\");
                    }
                    string pattern = string.Join(@"\b|\b", list) + @"\b";
                    MatchCollection mc = Regex.Matches(text, pattern);
                    foreach (Match match in mc)
                    {
                        for (int i = match.Index; i < match.Index + match.Length; i++)
                        {
                            groups[i].T = token.Key;
                        }
                    }
                }

            if (Language.RegexTokens != null)
                foreach (var token in Language.RegexTokens)
                {
                    MatchCollection mc = Regex.Matches(text, token.Value);
                    foreach (Match match in mc)
                    {
                        for (int i = match.Index; i < match.Index + match.Length; i++)
                        {
                            groups[i].T = token.Key;
                        }
                    }
                }

            return new(groups);
        }

        public IEnumerator<Char> GetEnumerator()
        {
            return Chars.GetEnumerator();
        }

        public int IndexOf(Char item)
        {
            return Chars.IndexOf(item);
        }

        public void Insert(int index, Char item)
        {
            Chars.Insert(index, item);
        }

        public bool Remove(Char item)
        {
            return Chars.Remove(item);
        }

        public void RemoveAt(int index)
        {
            Chars.RemoveAt(index);
        }

        internal int GetWordWrapStringFinishPosition(int v, Line line)
        {
            return 0;
        }

        internal int GetWordWrapStringIndex(int iChar)
        {
            return 0;
        }

        internal int GetWordWrapStringStartPosition(object v)
        {
            return 0;
        }

        private bool FoldableEnd(string text)
        {
            if (Language.FoldingPairs != null)
                foreach (SyntaxFolding syntaxFolding in Language.FoldingPairs)
                {
                    var match = Regex.Match(text, syntaxFolding.RegexEnd);
                    if (match.Success)
                    {
                        return true;
                    }
                }
            return false;
        }

        private bool FoldableStart(string text)
        {
            if (Language.FoldingPairs != null)
                foreach (SyntaxFolding syntaxFolding in Language.FoldingPairs)
                {
                    var match = Regex.Match(text, syntaxFolding.RegexStart);
                    if (match.Success)
                    {
                        return true;
                    }
                }
            return false;
        }
    }

    public class Place : IEquatable<Place>
    {
        public int iChar = 0;
        public int iLine = 0;

        public Place()
        {
        }

        public Place(Place oldplace)
        {
            this.iChar = oldplace.iChar;
            this.iLine = oldplace.iLine;
        }

        public Place(int iChar, int iLine)
        {
            this.iChar = iChar;
            this.iLine = iLine;
        }

        public static Place Empty
        {
            get { return new Place(); }
        }

        public static bool operator !=(Place p1, Place p2)
        {
            return !p1.Equals(p2);
        }

        public static Place operator +(Place p1, Place p2)
        {
            return new Place(p1.iChar + p2.iChar, p1.iLine + p2.iLine);
        }

        public static Place operator +(Place p1, int c2)
        {
            return new Place(p1.iChar + c2, p1.iLine);
        }
        public static Place operator -(Place p1, int c2)
        {
            return new Place(p1.iChar - c2, p1.iLine);
        }

        public static bool operator <(Place p1, Place p2)
        {
            if (p1.iLine < p2.iLine) return true;
            if (p1.iLine > p2.iLine) return false;
            if (p1.iChar < p2.iChar) return true;
            return false;
        }

        public static bool operator <=(Place p1, Place p2)
        {
            if (p1.Equals(p2)) return true;
            if (p1.iLine < p2.iLine) return true;
            if (p1.iLine > p2.iLine) return false;
            if (p1.iChar < p2.iChar) return true;
            return false;
        }

        public static bool operator ==(Place p1, Place p2)
        {
            return p1.Equals(p2);
        }

        public static bool operator >(Place p1, Place p2)
        {
            if (p1.iLine > p2.iLine) return true;
            if (p1.iLine < p2.iLine) return false;
            if (p1.iChar > p2.iChar) return true;
            return false;
        }

        public static bool operator >=(Place p1, Place p2)
        {
            if (p1.Equals(p2)) return true;
            if (p1.iLine > p2.iLine) return true;
            if (p1.iLine < p2.iLine) return false;
            if (p1.iChar > p2.iChar) return true;
            return false;
        }

        public bool Equals(Place other)
        {
            return iChar == other.iChar && iLine == other.iLine;
        }

        public override bool Equals(object obj)
        {
            return (obj is Place) && Equals((Place)obj);
        }

        public override int GetHashCode()
        {
            return iChar.GetHashCode() ^ iLine.GetHashCode();
        }

        public void Offset(int dx, int dy)
        {
            iChar += dx;
            iLine += dy;
        }

        public override string ToString()
        {
            return "(" + (iLine + 1) + "," + (iChar + 1) + ")";
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;

        public RelayCommand(Action execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException("execute");
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class SearchMatch
    {
        public int iChar { get; set; }
        public int iLine { get; set; }
        public string Match { get; set; }
    }

    public class Range : Bindable
    {
        public Range(Place place)
        {
            Start = place ?? new Place();
            End = place ?? new Place();
        }

        public Range(Place start, Place end)
        {
            Start = start;
            End = end;
        }

        public Range()
        {
        }

        public Place End { get => Get(new Place()); set => Set(value); }
        public Place Start { get => Get(new Place()); set => Set(value); }

        public Place VisualEnd { get => End > Start ? new(End) : new(Start); }
        public Place VisualStart { get => End > Start ? new(Start) : new(End); }
    }

    public class SyntaxError
    {
        public string Description { get; set; } = "";
        public int iChar { get; set; } = 0;
        public int iLine { get; set; } = 0;
        public SyntaxErrorType SyntaxErrorType { get; set; } = SyntaxErrorType.None;
        public string Title { get; set; } = "";
    }

    public class SyntaxFolding
    {
        public string RegexEnd { get; set; }
        public string RegexStart { get; set; }
    }

    public class WidthToThickness : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string culture)
        {
            double offset = (double)value;
            return new Thickness(0, offset, 0, offset);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string culture)
        {
            return 0;
        }
    }

    public class TokenToColor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string culture)
        {
            Token token = (Token)value;
            return EditorOptions.TokenColors[token];
        }

        public object ConvertBack(object value, Type targetType, object parameter, string culture)
        {
            return 0;
        }
    }
}