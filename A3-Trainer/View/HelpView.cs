using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>
    /// In-app help. Its content is the online help (help/index.html, embedded in the exe, see
    /// HelpDocument): one tab per section of the online help and a second row of tabs for its
    /// sub-tabs, so both helps always say the same.
    /// </summary>
    internal sealed class HelpView : UserControl
    {
        private readonly TabControl pages = new TabControl();

        // Built when the help is first shown, not with every main window.
        private bool loaded;

        /// <summary>Creates the help view; it fills itself from the embedded online help when first shown.</summary>
        internal HelpView()
        {
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.pages.SetBounds(4, 4, 622, 415);
            this.pages.ItemSize = new Size(80, 18);
            this.Controls.Add(this.pages);
            A3Theme.Apply(this);
        }

        /// <summary>Fills the tabs from the embedded online help, once.</summary>
        internal void EnsureHelpLoaded()
        {
            if (this.loaded)
                return;
            this.loaded = true;
            foreach (HelpDocument.Page page in HelpDocument.FromResource().Pages)
            {
                var tab = new TabPage(page.Title) { Padding = new Padding(4) };
                if (page.Sections.Count == 1)
                {
                    tab.Controls.Add(NewHelpBox(page.Sections[0].Value));
                }
                else
                {
                    var sections = new TabControl { Dock = DockStyle.Fill };
                    foreach (KeyValuePair<string, string> section in page.Sections)
                    {
                        var sectionTab = new TabPage(section.Key ?? page.Title) { Padding = new Padding(4) };
                        sectionTab.Controls.Add(NewHelpBox(section.Value));
                        sections.TabPages.Add(sectionTab);
                    }
                    tab.Controls.Add(sections);
                }
                this.pages.TabPages.Add(tab);
            }
            A3Theme.Apply(this);
        }

        /// <inheritdoc/>
        protected override void OnVisibleChanged(System.EventArgs e)
        {
            if (this.Visible)
                this.EnsureHelpLoaded();
            base.OnVisibleChanged(e);
        }

        private static RichTextBox NewHelpBox(string rtf) => new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = A3Theme.List,
            Rtf = rtf,
        };
    }
}
