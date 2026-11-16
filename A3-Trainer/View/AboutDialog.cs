using AnstossTrainer.Shared;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>About dialog: version, project links and a link to the log folder, like the A2G-Trainer's.</summary>
    internal sealed class AboutDialog : Form
    {
        internal const string GithubUrl = "https://github.com/transfairs/A3-Trainer";

        private readonly ToolTip tip = new ToolTip();

        /// <summary>Opens a link target (URL or folder) with the shell; tests replace it to avoid opening a browser.</summary>
        internal static Action<string> Open = target => Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });

        /// <summary>Shows why a link couldn't be opened; tests replace it so no message box blocks them.</summary>
        internal static Action<IWin32Window, string, string> ShowError = (owner, text, caption) => MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        /// <summary>The folder holding trainer.log (see Logger.A3.cs).</summary>
        internal static string LogFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Logger.AppFolder);

        /// <summary>The version the release pipeline stamped into Properties/VersionInfo.cs ("0.0.0-dev" in local builds).</summary>
        internal static string Version => ((AssemblyInformationalVersionAttribute)typeof(AboutDialog).Assembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion;

        /// <summary>Builds the dialog.</summary>
        internal AboutDialog()
        {
            this.Text = "Über A3-Trainer";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = SystemFonts.MessageBoxFont;
            this.ClientSize = new Size(480, 220);
            this.BackColor = A3Theme.Panel;

            var picture = new PictureBox { Name = "Logo", Image = LoadLogo(), SizeMode = PictureBoxSizeMode.Normal, BackColor = Color.Transparent };
            picture.SetBounds(12, 12, 64, 64);
            this.Controls.Add(picture);
            int x = 88;
            int w = this.ClientSize.Width - x - 12;

            var title = new Label { Name = "TitleLabel", Text = "A3-Trainer", Font = new Font(this.Font.FontFamily, this.Font.Size * 1.5f, FontStyle.Bold), AutoSize = false };
            title.SetBounds(x, 10, w - 110, 30);
            var version = new Label { Name = "VersionLabel", Text = "v" + Version, AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
            version.SetBounds(this.ClientSize.Width - 122, 14, 110, 22);
            var description = new Label { Text = "Echtzeit-Speichereditor für Anstoss 3 (Version 1.40).", AutoSize = false };
            description.SetBounds(x, 44, w, 20);
            this.Controls.AddRange(new Control[] { title, version, description });

            this.AddLink(x, 72, w, 20, "GitHub: " + GithubUrl, GithubUrl, GithubUrl);
            this.AddLink(x, 94, w, 20, "Onlinehilfe: " + MainWindow.HelpUrl, MainWindow.HelpUrl, MainWindow.HelpUrl);
            this.AddLink(x, 122, w, 36, "Protokoll:\n" + LogFolder, LogFolder, LogFolder);

            var ok = new Button { Name = "OkButton", Text = "OK", DialogResult = DialogResult.OK };
            ok.SetBounds(this.ClientSize.Width - 102, 180, 90, 28);
            this.Controls.Add(ok);
            this.AcceptButton = this.CancelButton = ok;
            A3Theme.Apply(this);
        }

        // The logo PNG scaled down to 64 px; it is embedded at build time, so the resource is always there.
        private static Bitmap LoadLogo()
        {
            var logo = new Bitmap(64, 64);
            using (Stream stream = typeof(AboutDialog).Assembly.GetManifestResourceStream("A3_Trainer.Resources.A3-Trainer-Logo.png"))
            using (Image source = Image.FromStream(stream))
            using (Graphics g = Graphics.FromImage(logo))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, 64, 64);
            }
            return logo;
        }

        // A label whose given parts (pairs of text and target) are clickable.
        private void AddLink(int x, int y, int w, int h, string text, params string[] parts)
        {
            var link = new LinkLabel
            {
                Text = text,
                AutoSize = false,
                AutoEllipsis = true,
                LinkColor = A3Theme.GreenDark,
                ActiveLinkColor = A3Theme.GreenLight,
                VisitedLinkColor = A3Theme.GreenDark,
            };
            link.Links.Clear();
            for (int i = 0; i < parts.Length; i += 2)
                link.Links.Add(text.IndexOf(parts[i], StringComparison.Ordinal), parts[i].Length, parts[i + 1]);
            link.SetBounds(x, y, w, h);
            link.LinkClicked += (s, e) => this.OpenLink(e.Link.LinkData as string);
            this.tip.SetToolTip(link, parts[1]);
            this.Controls.Add(link);
        }

        /// <summary>Opens a link target; a folder is created first, since the log folder only exists once something was logged.</summary>
        internal void OpenLink(string target)
        {
            if (string.IsNullOrEmpty(target))
                return;
            try
            {
                if (target == LogFolder)
                    Directory.CreateDirectory(target);
                Open(target);
            }
            catch (Exception ex)
            {
                Logger.Error("Link konnte nicht geöffnet werden: " + target, ex);
                ShowError(this, ex.Message, this.Text);
            }
        }

        /// <summary>Disposes the tooltip component along with the form.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this.tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
