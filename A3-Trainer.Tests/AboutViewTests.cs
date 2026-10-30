using AnstossTrainer.Shared.Tests;
using A3_Trainer.View;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    public class AboutViewTests
    {
        private static IEnumerable<Control> All(Control root) =>
            root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c)));

        [Fact]
        public void Version_ComesFromTheAssembly()
        {
            string stamped = typeof(AboutView).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            Assert.Equal("v" + stamped, AboutView.Version());
        }

        [Fact]
        public void Layout_ShowsVersionLinksAndLogo()
        {
            StaThread.Run(() =>
            {
                using (var view = new AboutView())
                {
                    List<Control> controls = All(view).ToList();
                    Assert.Contains(controls, c => c.Text == AboutView.Version());
                    List<string> urls = controls.OfType<LinkLabel>()
                        .SelectMany(l => l.Links.Cast<LinkLabel.Link>()).Select(l => (string)l.LinkData).ToList();
                    Assert.Equal(new[] { AboutView.GithubUrl, MainWindow.HelpUrl }, urls);
                    Assert.NotNull(controls.OfType<PictureBox>().Single().Image);
                }
            });
        }

        [Fact]
        public void Link_CoversOnlyTheLinkText()
        {
            StaThread.Run(() =>
            {
                using (var view = new AboutView())
                using (LinkLabel label = view.Link("Siehe Forum.", "Forum", "u", Rectangle.Empty))
                {
                    LinkLabel.Link link = label.Links.Cast<LinkLabel.Link>().Single();
                    Assert.Equal(6, link.Start);
                    Assert.Equal(5, link.Length);
                }
            });
        }

        [Fact]
        public void ClickingALink_OpensItsUrl_AndSwallowsLaunchErrors()
        {
            StaThread.Run(() =>
            {
                using (var view = new AboutView())
                using (LinkLabel label = view.Link("x", "x", "https://example.invalid/", Rectangle.Empty))
                {
                    string opened = null;
                    view.StartProcess = url => opened = url;
                    Click(label);
                    Assert.Equal("https://example.invalid/", opened);

                    view.StartProcess = url => throw new InvalidOperationException("kein Browser");
                    Click(label); // logged, not thrown
                }
            });
        }

        private static void Click(LinkLabel label) =>
            typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(label, new object[] { new LinkLabelLinkClickedEventArgs(label.Links[0]) });
    }
}
