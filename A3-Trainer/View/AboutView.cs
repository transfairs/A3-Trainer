using AnstossTrainer.Shared;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>About page: version and project links, laid out like the A2G-Trainer's.</summary>
    internal sealed class AboutView : UserControl
    {
        internal const string GithubUrl = "https://github.com/transfairs/A3-Trainer";

        private readonly Label version = new Label();

        /// <summary>Opens the URL in the browser; tests replace it to avoid launching one.</summary>
        internal Action<string> StartProcess = url => Process.Start(url);

        /// <summary>Builds the page: logo, version and links.</summary>
        internal AboutView()
        {
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        /// <summary>The version the release pipeline stamped into Properties/VersionInfo.cs.</summary>
        internal static string Version()
        {
            var info = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute));
            return "v" + info.InformationalVersion;
        }

        private void BuildLayout()
        {
            var about = new GroupBox { Text = "Über", Bounds = new Rectangle(8, 8, 390, 104) };
            about.Controls.Add(new Label
            {
                Text = "A3 - Trainer",
                Font = new Font("Microsoft Sans Serif", 10f),
                AutoSize = true,
                Location = new Point(10, 18),
            });
            this.version.Text = Version();
            this.version.Font = new Font("Microsoft Sans Serif", 7f);
            this.version.TextAlign = ContentAlignment.TopRight;
            this.version.SetBounds(250, 22, 130, 14);
            about.Controls.Add(this.version);

            int y = 48;
            void Line(string text, string linkText, string url)
            {
                about.Controls.Add(Link(text, linkText, url, new Rectangle(10, y, 370, 18)));
                y += 26;
            }
            Line("Github: " + GithubUrl, GithubUrl, GithubUrl);
            Line("Onlinehilfe: " + MainWindow.HelpUrl, MainWindow.HelpUrl, MainWindow.HelpUrl);
            this.Controls.Add(about);

            var logo = new PictureBox
            {
                Image = Logo(),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(414, 8, 200, 200),
            };
            this.Controls.Add(logo);
        }

        /// <summary>A label whose <paramref name="linkText"/> part opens <paramref name="url"/>.</summary>
        internal LinkLabel Link(string text, string linkText, string url, Rectangle bounds)
        {
            var label = new LinkLabel
            {
                Text = text,
                Bounds = bounds,
                LinkColor = A3Theme.GreenDark,
                ActiveLinkColor = A3Theme.GreenLight,
                VisitedLinkColor = A3Theme.GreenDark,
                UseCompatibleTextRendering = true,
            };
            label.Links.Clear();
            label.Links.Add(text.IndexOf(linkText, StringComparison.Ordinal), linkText.Length, url);
            label.LinkClicked += (s, e) => this.Open((string)e.Link.LinkData);
            return label;
        }

        // The logo is embedded at build time, so the resource is always there.
        private static Image Logo()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("A3_Trainer.Resources.A3-Trainer-Logo.png"))
            using (Image image = Image.FromStream(stream))
                return new Bitmap(image); // copy: GDI+ keeps reading the stream otherwise
        }

        private void Open(string url)
        {
            try
            {
                this.StartProcess(url);
            }
            catch (Exception ex)
            {
                Logger.Error("Link konnte nicht geöffnet werden: " + url, ex);
            }
        }
    }
}
