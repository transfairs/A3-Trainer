using A3_Trainer.View;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>The About dialog: version, links, log folder.</summary>
    public class AboutDialogTests
    {
        [Fact]
        public void Version_ComesFromTheAssembly()
        {
            string stamped = typeof(AboutDialog).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            Assert.Equal(stamped, AboutDialog.Version);
        }

        [Fact]
        public void Layout_ShowsVersionLinksAndLogo() => StaThread.Run(() =>
        {
            using (var dialog = new AboutDialog())
            {
                List<Control> controls = dialog.Controls.Cast<Control>().ToList();
                Assert.Equal("v" + AboutDialog.Version, controls.Single(c => c.Name == "VersionLabel").Text);
                List<string> targets = controls.OfType<LinkLabel>()
                    .SelectMany(l => l.Links.Cast<LinkLabel.Link>()).Select(l => (string)l.LinkData).ToList();
                Assert.Equal(new[] { AboutDialog.GithubUrl, MainWindow.HelpUrl, AboutDialog.LogFolder }, targets);
                Assert.NotNull(controls.OfType<PictureBox>().Single().Image);
                Assert.Same(dialog.AcceptButton, dialog.CancelButton);
                Assert.EndsWith("A3-Trainer", AboutDialog.LogFolder);
            }
        });

        [Fact]
        public void OpenLink_OpensTargets_CreatesTheLogFolder_AndReportsErrors() => StaThread.Run(() =>
        {
            Action<string> open = AboutDialog.Open;
            Action<IWin32Window, string, string> showError = AboutDialog.ShowError;
            var opened = new List<string>();
            var errors = new List<string>();
            try
            {
                AboutDialog.Open = opened.Add;
                AboutDialog.ShowError = (owner, text, caption) => errors.Add(text);
                using (var dialog = new AboutDialog())
                {
                    dialog.OpenLink(null);
                    dialog.OpenLink(AboutDialog.GithubUrl);
                    dialog.OpenLink(AboutDialog.LogFolder);
                    Assert.Equal(new[] { AboutDialog.GithubUrl, AboutDialog.LogFolder }, opened);
                    Assert.True(Directory.Exists(AboutDialog.LogFolder));

                    LinkLabel github = dialog.Controls.OfType<LinkLabel>().First();
                    TestHelpers.Invoke(github, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(github.Links[0]));
                    Assert.Equal(3, opened.Count);

                    AboutDialog.Open = target => throw new InvalidOperationException("kein Browser");
                    dialog.OpenLink(AboutDialog.GithubUrl);
                    Assert.Equal(new[] { "kein Browser" }, errors);
                }
            }
            finally
            {
                AboutDialog.Open = open;
                AboutDialog.ShowError = showError;
            }
        });
    }
}
