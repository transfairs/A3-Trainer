using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A page made of views on tabs, such as the own club with its identity and stadium.</summary>
    internal sealed class PagesView : UserControl
    {
        private readonly TabControl tabs = new TabControl();

        /// <summary>An empty page; <see cref="Add"/> puts the views on it.</summary>
        internal PagesView()
        {
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.tabs.SetBounds(4, 4, 622, 415);
            this.tabs.ItemSize = new Size(80, 18);
            this.Controls.Add(this.tabs);
            A3Theme.Apply(this);
        }

        /// <summary>Adds <paramref name="view"/> on a tab named <paramref name="title"/>.</summary>
        internal PagesView Add(string title, UserControl view)
        {
            var page = new TabPage(title);
            view.Location = Point.Empty;
            view.Size = new Size(614, 389);
            page.Controls.Add(view);
            this.tabs.TabPages.Add(page);
            A3Theme.Apply(page);
            return this;
        }
    }
}
