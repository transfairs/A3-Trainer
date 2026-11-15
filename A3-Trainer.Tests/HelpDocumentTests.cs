using AnstossTrainer.Shared.Tests;
using System.Linq;
using System.Windows.Forms;
using A3_Trainer.View;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>Tests for HelpDocument, which turns the embedded online help into the in-app help.</summary>
    public class HelpDocumentTests
    {
        [Fact]
        public void FromResource_HasEveryPanelOfTheOnlineHelpButChangelogAndAbout()
        {
            HelpDocument help = HelpDocument.FromResource();

            Assert.Equal(new[] { "Erste Schritte", "Spieler", "Verein & Manager", "Spielmechaniken", "Fehlerbehebung / FAQ" },
                help.Pages.Select(p => p.Title));
            HelpDocument.Page players = help.Pages.Single(p => p.Title == "Spieler");
            Assert.Contains("Wechsel", players.Sections.Select(s => s.Key));
            Assert.Single(help.Pages.First().Sections);
        }

        [Fact]
        public void Parse_RendersTheHelpsElements_AndSkipsScriptsButtonsAndImages()
        {
            const string html = @"<nav><a href=""#a"" data-target=""a"">Alpha &amp; Co</a></nav>
<section class=""panel"" id=""panel-a""><div class=""panel-header""><h1>Titel</h1><p class=""lead"">Ein <strong>fetter</strong> {Text}\</p></div>
<div class=""subtabs""><button data-sub=""x"">Erster</button><button data-sub=""y"">Zweiter</button></div>
<div class=""subpanel active"" data-sub=""x""><fieldset class=""groupbox""><legend>Box</legend><dl class=""field-list""><dt>Feld</dt><dd>Grün <em>kursiv</em></dd></dl></fieldset></div>
<div class=""subpanel"" data-sub=""y""><div class=""callout""><strong class=""label"">Hinweis</strong> Text<img src=""x.png""></div><script>var x = 1;</script><ul><li>Punkt</li></ul></div>
</section>
<section class=""panel"" id=""panel-changelog""><p>weg</p></section>";

            HelpDocument help = HelpDocument.Parse(html);

            HelpDocument.Page page = Assert.Single(help.Pages);
            Assert.Equal("Alpha & Co", page.Title);
            Assert.Equal(new[] { "Erster", "Zweiter" }, page.Sections.Select(s => s.Key));
            string first = page.Sections[0].Value;
            Assert.Contains(@"{\fs28\b Titel}", first);
            Assert.Contains(@"{\b fetter}", first);
            Assert.Contains(@"\{Text\}\\", first);
            // A field's name leads its description in the same paragraph; headings get a rule.
            Assert.Contains(@"{\b Feld:} Gr\u252?n {\i kursiv}\par", first);
            Assert.Contains(@"{\fs24\b Box}\par\pard\sa120 ---", first);
            string second = page.Sections[1].Value;
            Assert.DoesNotContain("Titel", second);
            Assert.Contains(@"{\b\cf2 Hinweis:}", second);
            Assert.DoesNotContain("var x", second);
            Assert.Contains(@"\bullet  Punkt", second);
        }

        [Fact]
        public void EveryPage_IsValidRtf() => StaThread.Run(() =>
        {
            using (RichTextBox box = new RichTextBox())
            {
                foreach (var section in HelpDocument.FromResource().Pages.SelectMany(p => p.Sections))
                {
                    box.Rtf = section.Value;
                    Assert.False(string.IsNullOrWhiteSpace(box.Text));
                }
            }
        });

        [Fact]
        public void HelpView_ShowsOneTabPerPage_WithSubTabsWhereTheOnlineHelpHasThem() => StaThread.Run(() =>
        {
            using (HelpView view = new HelpView())
            {
                // A second call keeps the tabs as they are.
                view.EnsureHelpLoaded();
                view.EnsureHelpLoaded();
                TabControl main = TestHelpers.Get<TabControl>(view, "pages");
                Assert.Equal(5, main.TabPages.Count);
                TabPage players = main.TabPages.Cast<TabPage>().Single(t => t.Text == "Spieler");
                TabControl sections = Assert.IsType<TabControl>(Assert.Single(players.Controls.Cast<Control>()));
                Assert.Contains("Wechsel", sections.TabPages.Cast<TabPage>().Select(t => t.Text));
                Assert.IsType<RichTextBox>(Assert.Single(main.TabPages[0].Controls.Cast<Control>()));
            }
        });
    }
}
