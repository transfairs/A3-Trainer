using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>The own club's page: the club and its stadium as tabs.</summary>
    internal sealed class ClubPagesView : UserControl
    {
        private readonly TabControl tabs = new TabControl();

        /// <summary>Puts <paramref name="club"/> and <paramref name="stadium"/> on a tab each.</summary>
        internal ClubPagesView(ClubView club, StadiumView stadium)
        {
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.tabs.SetBounds(4, 4, 622, 415);
            this.tabs.ItemSize = new Size(80, 18);
            this.AddPage("Verein", club);
            this.AddPage("Stadion", stadium);
            this.Controls.Add(this.tabs);
            A3Theme.Apply(this);
        }

        private void AddPage(string title, UserControl view)
        {
            var page = new TabPage(title);
            view.Location = Point.Empty;
            view.Size = new Size(614, 389);
            page.Controls.Add(view);
            this.tabs.TabPages.Add(page);
        }
    }
}
