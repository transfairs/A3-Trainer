using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>
    /// Anstoss 3's look: lilac-grey panels with the game's bright green headers and bars. The
    /// colours are sampled from the game's own screens (squad list, training plan).
    /// </summary>
    internal static class A3Theme
    {
        /// <summary>Screen background.</summary>
        internal static readonly Color Panel = Color.FromArgb(0xC6, 0xBA, 0xC6);
        /// <summary>List and input background.</summary>
        internal static readonly Color List = Color.FromArgb(0xE7, 0xDF, 0xE7);
        /// <summary>Read-only info fields.</summary>
        internal static readonly Color Field = Color.FromArgb(0xAD, 0xAA, 0xB5);
        /// <summary>Headers, buttons and the menu bar.</summary>
        internal static readonly Color Green = Color.FromArgb(0x73, 0xCB, 0x39);
        /// <summary>Highlights.</summary>
        internal static readonly Color GreenLight = Color.FromArgb(0x8C, 0xDF, 0x29);
        /// <summary>Pressed buttons and borders.</summary>
        internal static readonly Color GreenDark = Color.FromArgb(0x4A, 0x9A, 0x21);
        /// <summary>Dark text.</summary>
        internal static readonly Color Text = Color.FromArgb(0x39, 0x34, 0x39);

        /// <summary>Applies the look to a control tree (call after the controls are created).</summary>
        internal static void Apply(Control root)
        {
            root.ForeColor = Text;
            Style(root);
            foreach (Control child in root.Controls)
                Apply(child);
        }

        private static void Style(Control control)
        {
            switch (control)
            {
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Green;
                    button.FlatAppearance.BorderColor = GreenDark;
                    button.FlatAppearance.MouseOverBackColor = GreenLight;
                    button.FlatAppearance.MouseDownBackColor = GreenDark;
                    break;
                case ListView list:
                    list.BackColor = List;
                    list.OwnerDraw = true;
                    list.DrawColumnHeader -= DrawGreenHeader;
                    list.DrawColumnHeader += DrawGreenHeader;
                    list.DrawItem -= DrawDefaultItem;
                    list.DrawItem += DrawDefaultItem;
                    list.DrawSubItem -= DrawDefaultSubItem;
                    list.DrawSubItem += DrawDefaultSubItem;
                    break;
                case TabControl tabs:
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tabs.DrawItem -= DrawGreenTab;
                    tabs.DrawItem += DrawGreenTab;
                    break;
                case TabPage page:
                    page.BackColor = Panel;
                    page.UseVisualStyleBackColor = false;
                    break;
                case CheckedListBox checkedList:
                    checkedList.BackColor = List;
                    break;
                case TextBox text:
                    text.BackColor = text.ReadOnly ? Field : List;
                    break;
                case ComboBox combo:
                    combo.BackColor = List;
                    combo.FlatStyle = FlatStyle.Flat;
                    break;
                case NumericUpDown number:
                    number.BackColor = List;
                    break;
                case GroupBox _:
                case Panel _:
                case UserControl _:
                    control.BackColor = Panel;
                    break;
            }
        }

        private static void DrawGreenHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var back = new SolidBrush(Green))
                e.Graphics.FillRectangle(back, e.Bounds);
            using (var line = new Pen(GreenDark))
                e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font, e.Bounds, Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.LeftAndRightPadding);
        }

        private static void DrawDefaultItem(object sender, DrawListViewItemEventArgs e) => e.DrawDefault = true;

        private static void DrawDefaultSubItem(object sender, DrawListViewSubItemEventArgs e) => e.DrawDefault = true;

        private static void DrawGreenTab(object sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender;
            bool selected = e.Index == tabs.SelectedIndex;
            using (var back = new SolidBrush(selected ? Green : Panel))
                e.Graphics.FillRectangle(back, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds, Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }

        /// <summary>Green menu bar with green highlights, matching the game.</summary>
        internal static void ApplyMenu(MenuStrip menu)
        {
            menu.BackColor = Green;
            menu.ForeColor = Text;
            menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => Green;
            public override Color MenuStripGradientEnd => Green;
            public override Color MenuItemSelected => GreenLight;
            public override Color MenuItemSelectedGradientBegin => GreenLight;
            public override Color MenuItemSelectedGradientEnd => GreenLight;
            public override Color MenuItemPressedGradientBegin => Panel;
            public override Color MenuItemPressedGradientEnd => Panel;
            public override Color MenuItemBorder => GreenDark;
            public override Color MenuBorder => GreenDark;
            public override Color ToolStripDropDownBackground => Panel;
            public override Color ImageMarginGradientBegin => Panel;
            public override Color ImageMarginGradientMiddle => Panel;
            public override Color ImageMarginGradientEnd => Panel;
            public override Color SeparatorDark => GreenDark;
        }
    }
}
