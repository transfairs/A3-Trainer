using A3_Trainer.Controller;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>The main window: menu, pages, waiting screen, online help and About.</summary>
    public class MainWindowTests
    {
        // The window with its game (and the views' games) reading from the fake memory instead of
        // a real process; attached means the test process stands in for the game.
        private static MainWindow Window(FakeGameMemory m, bool attached = true)
        {
            var window = new MainWindow();
            var game = new A3Game(m);
            TestHelpers.Set(window, "game", game);
            foreach (string view in new[] { "playerView", "clubView", "managerView" })
                TestHelpers.Set(TestHelpers.Get<object>(window, view), "game", game);
            if (attached)
                TestHelpers.Get<ProcessMemory>(window, "memory").mProc.Process = Process.GetCurrentProcess();
            return window;
        }

        private static FakeGameMemory WithOpponent()
        {
            FakeGameMemory m = A3GameTests.Career();
            A3GameTests.PutPlayer(m, 1039, 14, 1, 1, 11);
            A3GameTests.PutPlayer(m, 1255, 14, 10, 1, 10);
            A3GameTests.PutCache(m, 1039, 1255);
            return m;
        }

        private static ToolStripItem MenuItem(MainWindow window, string menu, string item) =>
            window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().Single(i => i.Text == menu)
                .DropDownItems.Cast<ToolStripItem>().Single(i => i.Text == item);

        private static Control Shown(MainWindow window) => TestHelpers.Get<Panel>(window, "content").Controls.Cast<Control>().Single();

        [Fact]
        public void Starts_OnTheWaitingScreen() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(A3GameTests.Career()))
            {
                Assert.Equal("A3-Trainer", window.Text);
                Assert.Same(TestHelpers.Get<Label>(window, "waiting"), Shown(window));
                Assert.NotNull(window.Icon);
            }
        });

        [Fact]
        public void Menu_ShowsEachPageWithItsTitle() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(WithOpponent()))
            {
                TestHelpers.Click(MenuItem(window, "Verein", "Eigener Verein"));
                Assert.IsType<ClubView>(Shown(window));
                Assert.Equal("A3-Trainer :: Verein: 1.FV München", window.Text);

                TestHelpers.Click(MenuItem(window, "Trainer", "Manager (Spieler 1)"));
                Assert.IsType<ManagerView>(Shown(window));
                Assert.Equal("A3-Trainer :: Manager (Spieler 1)", window.Text);

                TestHelpers.Click(MenuItem(window, "Spieler", "Nächster Gegner"));
                Assert.IsType<PlayerView>(Shown(window));
                Assert.Equal("A3-Trainer :: Nächster Gegner: Hamburger FC", window.Text);

                TestHelpers.Click(MenuItem(window, "Spieler", "Eigener Kader"));
                Assert.Equal("A3-Trainer :: Eigener Kader: 1.FV München", window.Text);

                TestHelpers.Click(MenuItem(window, "?", "Über"));
                Assert.IsType<AboutView>(Shown(window));
                Assert.Equal("A3-Trainer :: Über", window.Text);
            }
        });

        [Fact]
        public void Opponent_WhenNoneIsLoaded_SaysSo() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(A3GameTests.Career()))
            {
                TestHelpers.Click(MenuItem(window, "Spieler", "Nächster Gegner"));

                Assert.Equal("A3-Trainer :: Nächster Gegner (keiner geladen)", window.Text);
            }
        });

        [Fact]
        public void GameEvents_SwitchBetweenTheLastPageAndTheWaitingScreen() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(WithOpponent()))
            {
                TestHelpers.Click(MenuItem(window, "Trainer", "Manager (Spieler 1)"));

                window.OnGameLost();
                Assert.IsType<Label>(Shown(window));
                Assert.Equal("A3-Trainer", window.Text);

                window.ShowStatus("Nicht unterstützt");
                Assert.Equal("Nicht unterstützt", Shown(window).Text);

                window.OnGameAttached();
                Assert.IsType<ManagerView>(Shown(window));
            }
        });

        [Fact]
        public void Pages_WithoutAGameOrCareer_ShowTheWaitingScreen() => StaThread.Run(() =>
        {
            using (MainWindow detached = Window(A3GameTests.Career(), attached: false))
            {
                TestHelpers.Click(MenuItem(detached, "Verein", "Eigener Verein"));
                Assert.IsType<Label>(Shown(detached));
            }

            FakeGameMemory noCareer = A3GameTests.Career();
            noCareer.PutUInt16(A3Addresses.ManagerClubNumber, 2);
            using (MainWindow window = Window(noCareer))
            {
                TestHelpers.Click(MenuItem(window, "Verein", "Eigener Verein"));
                Assert.IsType<Label>(Shown(window));
            }
        });

        [Fact]
        public void OnlineHelp_OpensTheHelpPage_OrSaysWhereItIs() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(A3GameTests.Career()))
            {
                string opened = null;
                window.StartProcess = url => opened = url;
                TestHelpers.Click(MenuItem(window, "?", "Onlinehilfe"));
                Assert.Equal(MainWindow.HelpUrl, opened);

                window.StartProcess = url => throw new InvalidOperationException("kein Browser");
                TestHelpers.ExpectDialog("Hilfe", () => TestHelpers.Click(MenuItem(window, "?", "Onlinehilfe")));
            }
        });
    }
}
