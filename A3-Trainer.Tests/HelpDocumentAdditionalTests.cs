using AnstossTrainer.Shared.Tests;
using System.Linq;
using System.Windows.Forms;
using A3_Trainer.View;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>
    /// Tests for HelpDocument's handling of HTML the embedded help doesn't (yet) contain: every
    /// rendered element, untitled panels and sub-tabs, stray markup outside a panel and tags
    /// closed out of order; and HelpView filling itself when it becomes visible.
    /// </summary>
    public class HelpDocumentAdditionalTests
    {
        [Fact]
        public void Parse_RendersEveryElementItKnows()
        {
            const string html = @"<section class=""panel"" id=""panel-alles"">
<h1>Eins</h1><h2>Zwei</h2><h3>Drei</h3><fieldset><legend>Box</legend></fieldset>
<dl><dt>Feld</dt><dd>Wert</dd></dl><ul><li>Punkt</li></ul>
<div class=""callout warn""><b class=""label"">Achtung</b> rot</div>
<div class=""callout tip""><strong class=""label"">Tipp</strong> gr&uuml;n</div>
<div class=""callout""><b class=""label"">Hinweis</b> blau</div>
<div class=""plain""><b>fett</b> <i>schr&auml;g</i> <em>betont</em> <code>a.exe</code></div>
<span class=""menu-path"">Datei &gt; Laden</span><span class=""chip"">Chip</span><span>schlicht</span> <a href=""#x"">Link</a>
<p>Zeile<br>neue Zeile</p>
</section>";

            HelpDocument.Page page = Assert.Single(HelpDocument.Parse(html).Pages);
            string rtf = Assert.Single(page.Sections).Value;

            // Not in the navigation: the panel's id is its title.
            Assert.Equal("alles", page.Title);
            Assert.Null(page.Sections[0].Key);
            Assert.Contains(@"{\fs28\b Eins}\par ", rtf);
            Assert.Contains(@"{\fs24\b Zwei}\par\pard\sa120 ---", rtf);
            Assert.Contains(@"{\fs24\b Drei}\par\pard\sa120 ---", rtf);
            Assert.Contains(@"{\fs24\b Box}\par\pard\sa120 ---", rtf);
            Assert.Contains(@"\pard\qj\sa120{\b Feld:} Wert\par ", rtf);
            Assert.Contains(@"\pard\li360\fi-200\sa60 \bullet  Punkt\par ", rtf);
            Assert.Contains(@"{\b\cf1 Achtung:}  rot\par ", rtf);
            Assert.Contains(@"{\b\cf3 Tipp:}  gr\u252?n\par ", rtf);
            Assert.Contains(@"{\b\cf2 Hinweis:}  blau\par ", rtf);
            Assert.Contains(@"{\b fett} {\i schr\u228?g} {\i betont} {\f1 a.exe}", rtf);
            Assert.Contains(@"{\b Datei > Laden}\bullet  Chip   schlicht Link", rtf);
            Assert.Contains(@"\pard\qj\sa120 Zeile\line neue Zeile\par ", rtf);

            using (RichTextBox box = new RichTextBox())
            {
                box.Rtf = rtf;
                Assert.Contains("neue Zeile", box.Text);
            }
        }

        [Fact]
        public void Parse_IgnoresMarkupOutsidePanels_AndClosesTagsLeftOpen()
        {
            const string html = @"<h1>Vor allem</h1><p>Einleitung<br></p>
<section class=""panel""><p>Ein <b>offenes <i>Ende</p><p>Danach</p>
<nav>Navigation<br>weg</nav></section>";

            HelpDocument.Page page = Assert.Single(HelpDocument.Parse(html).Pages);
            string rtf = Assert.Single(page.Sections).Value;

            // A panel without id has an empty one, and so an empty title.
            Assert.Equal("", page.Title);
            Assert.DoesNotContain("Vor allem", rtf);
            Assert.DoesNotContain("Einleitung", rtf);
            // </p> also closes the <i> and <b> still open in it, innermost first.
            Assert.Contains(@"\pard\qj\sa120 Ein {\b offenes {\i Ende}}\par \pard\qj\sa120 Danach\par ", rtf);
            Assert.DoesNotContain("Navigation", rtf);
            Assert.DoesNotContain(@"\line", rtf);
        }

        [Fact]
        public void Parse_SubTabsWithoutButton_ComeLastAndUntitled()
        {
            const string html = @"<section class=""panel"" id=""panel-p"">
<div class=""subtabs""><button data-sub=""a"">Erster</button></div>
<div class=""subpanel"" data-sub=""b""><p>Ohne Knopf</p></div>
<div class=""subpanel"" data-sub=""a""><p>Mit Knopf</p></div>
</section>";

            HelpDocument.Page page = Assert.Single(HelpDocument.Parse(html).Pages);

            Assert.Equal(new[] { "Erster", null }, page.Sections.Select(s => s.Key));
            Assert.Contains("Mit Knopf", page.Sections[0].Value);
            Assert.Contains("Ohne Knopf", page.Sections[1].Value);
        }

        [Fact]
        public void HelpView_FillsItselfWhenItBecomesVisible() => StaThread.Run(() =>
        {
            using (HelpView view = new HelpView())
            {
                TabControl main = TestHelpers.Get<TabControl>(view, "pages");
                view.Visible = false;
                Assert.Empty(main.TabPages);

                view.Visible = true;

                Assert.Equal(HelpDocument.FromResource().Pages.Count, main.TabPages.Count);
            }
        });
    }
}
