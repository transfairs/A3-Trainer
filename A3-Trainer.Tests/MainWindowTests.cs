using A3_Trainer.Controller;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Drawing;
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
            foreach (string view in new[] { "playerView", "clubView", "identityView", "stadiumView", "boardView", "financesView", "investmentsView", "managerView", "personView", "careerView", "stocksView" })
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
                Assert.IsType<PagesView>(Shown(window));
                Assert.Equal("A3-Trainer :: Verein: 1.FV München", window.Text);

                TestHelpers.Click(MenuItem(window, "Trainer", "Manager"));
                Assert.Same(TestHelpers.Get<PagesView>(window, "managerPages"), Shown(window));
                Assert.Equal("A3-Trainer :: Manager (Spieler 1)", window.Text);

                TestHelpers.Click(MenuItem(window, "Spieler", "Nächster Gegner"));
                Assert.IsType<PlayerView>(Shown(window));
                Assert.Equal("A3-Trainer :: Nächster Gegner: Hamburger FC", window.Text);

                TestHelpers.Click(MenuItem(window, "Spieler", "Eigener Kader"));
                Assert.Equal("A3-Trainer :: Eigener Kader: 1.FV München", window.Text);

                TestHelpers.Click(MenuItem(window, "?", "Hilfe"));
                Assert.IsType<HelpView>(Shown(window));
                Assert.Equal("A3-Trainer :: Hilfe", window.Text);
                Assert.True(TestHelpers.Get<TabControl>(Shown(window), "pages").TabCount > 0);

                // About is a dialog over the page shown.
                Form shownDialog = null;
                window.ShowDialogBox = dialog => { shownDialog = dialog; return DialogResult.OK; };
                TestHelpers.Click(MenuItem(window, "?", "Über"));
                Assert.IsType<AboutDialog>(shownDialog);
                Assert.Equal("A3-Trainer :: Hilfe", window.Text);
            }
        });

        [Fact]
        public void History_GoesBackAndForward_ThroughMenuButtonsAndAltArrows() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(WithOpponent()))
            {
                ToolStripItem back = window.MainMenuStrip.Items[0];
                ToolStripItem forward = window.MainMenuStrip.Items[1];
                Assert.Equal("◀", back.Text);
                Assert.False(back.Enabled);

                window.OnGameAttached(); // the squad page is the first entry
                TestHelpers.Click(MenuItem(window, "Verein", "Eigener Verein"));
                TestHelpers.Click(MenuItem(window, "?", "Hilfe"));
                Assert.True(back.Enabled);
                Assert.False(forward.Enabled);

                TestHelpers.Click(back);
                Assert.Equal("A3-Trainer :: Verein: 1.FV München", window.Text);
                Assert.True(window.GoBack());
                Assert.Equal("A3-Trainer :: Eigener Kader: 1.FV München", window.Text);
                Assert.False(window.GoBack());

                TestHelpers.Click(forward);
                Assert.Equal("A3-Trainer :: Verein: 1.FV München", window.Text);
                Message msg = default(Message);
                Assert.True((bool)TestHelpers.Invoke(window, "ProcessCmdKey", msg, Keys.Alt | Keys.Right));
                Assert.Equal("A3-Trainer :: Hilfe", window.Text);

                // A new page drops what lay ahead.
                window.GoBack();
                TestHelpers.Click(MenuItem(window, "Trainer", "Manager"));
                Assert.False(window.CanGoForward);
            }
        });

        [Fact]
        public void History_KeepsTheClubPickedInThePlayerView() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(WithOpponent()))
            {
                TestHelpers.Click(MenuItem(window, "Spieler", "Eigener Kader"));
                TestHelpers.Invoke(window, "OnClubChosen", new A3_Trainer.Model.ClubKey(14, 10));
                Assert.Equal("A3-Trainer :: Verein: Hamburger FC", window.Text);
                TestHelpers.Click(MenuItem(window, "Verein", "Eigener Verein"));

                window.GoBack();
                Assert.Equal("A3-Trainer :: Verein: Hamburger FC", window.Text);
                window.GoBack();
                Assert.Equal("A3-Trainer :: Eigener Kader: 1.FV München", window.Text);
            }
        });

        [Fact]
        public void ClubPage_PicksAnyClub_AndAllTabsFollowIt() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(ClubSelectionTests.Clubs()))
            {
                TestHelpers.Click(MenuItem(window, "Verein", "Anderer Verein")); // nothing picked yet: the own club
                Assert.Equal("A3-Trainer :: Verein: 1.FV München", window.Text);
                ComboBox countries = TestHelpers.Get<ComboBox>(window, "clubCountryBox");
                ComboBox clubs = TestHelpers.Get<ComboBox>(window, "clubClubBox");
                Assert.Equal("Deutschland", countries.Text);
                Assert.Equal("1.FV München", clubs.Text);
                Assert.Same(TestHelpers.Get<PagesView>(window, "clubPages"), countries.Parent);

                clubs.SelectedIndex = 1;
                var hamburg = new A3_Trainer.Model.ClubKey(14, 10);
                Assert.Equal("A3-Trainer :: Verein: Hamburger FC", window.Text);
                Assert.Equal(hamburg, TestHelpers.Get<ClubView>(window, "clubView").Club);
                Assert.Equal(hamburg, TestHelpers.Get<IdentityView>(window, "identityView").Club);
                Assert.Equal(hamburg, TestHelpers.Get<StadiumView>(window, "stadiumView").Club);
                Assert.Equal("Hamburger FC", TestHelpers.Get<TextBox>(TestHelpers.Get<ClubView>(window, "clubView"), "clubName").Text);

                countries.SelectedIndex = 0; // Dänemark
                Assert.Equal("A3-Trainer :: Verein: Aarhus IL", window.Text);

                // The own club comes back from the menu and the history, the picked one stays for "Anderer Verein".
                TestHelpers.Click(MenuItem(window, "Verein", "Eigener Verein"));
                Assert.Equal("1.FV München", clubs.Text);
                window.GoBack();
                Assert.Equal("A3-Trainer :: Verein: Aarhus IL", window.Text);
                window.GoBack();
                Assert.Equal("A3-Trainer :: Verein: Hamburger FC", window.Text);
                TestHelpers.Click(MenuItem(window, "Verein", "Anderer Verein"));
                Assert.Equal("A3-Trainer :: Verein: Hamburger FC", window.Text);

                // The player page's choice carries over.
                TestHelpers.Click(MenuItem(window, "Spieler", "Anderer Verein"));
                Assert.Equal("A3-Trainer :: Verein: Hamburger FC", window.Text);
                Assert.Equal(hamburg, TestHelpers.Get<A3_Trainer.Model.ClubKey?>(window, "otherClub"));
            }
        });

        [Fact]
        public void Mechanics_InterviewEffects_SwitchTheTimerAndLabelWhileTheGameRuns() => StaThread.Run(() =>
        {
            FakeGameMemory m = InterviewEffectsTests.WithInterview(out uint[] slots);
            using (MainWindow window = Window(m, attached: false))
            {
                var effects = new InterviewEffects(m, new TransferInjectorTests.FakeCode(m));
                TestHelpers.Set(window, "interviewEffects", effects);
                var item = (ToolStripMenuItem)MenuItem(window, "Spielmechaniken", "Interviews: Wirkung der Antworten zeigen");
                Timer timer = TestHelpers.Get<Timer>(window, "mechanicsTimer");
                uint original = m.ReadUInt32(slots[0]);

                item.Checked = true;                   // no game yet: only the timer runs
                Assert.True(timer.Enabled);
                TestHelpers.Invoke(window, "TickMechanics");
                Assert.Equal(original, m.ReadUInt32(slots[0]));

                TestHelpers.Get<ProcessMemory>(window, "memory").mProc.Process = Process.GetCurrentProcess();
                TestHelpers.Invoke(window, "TickMechanics");
                Assert.NotEqual(original, m.ReadUInt32(slots[0]));

                item.Checked = false;
                Assert.False(timer.Enabled);
                Assert.Equal(original, m.ReadUInt32(slots[0]));

                TestHelpers.Get<ProcessMemory>(window, "memory").mProc.Process = null;
                item.Checked = true;
                item.Checked = false;
                window.OnGameLost();
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
        public void HotSeat_FollowsTheTurn_AndTheMenuPicksAnotherManager() => StaThread.Run(() =>
        {
            FakeGameMemory m = A3GameEdgeTests.HotSeat();
            m.Put(A3Addresses.ManagerFirstName, System.Text.Encoding.ASCII.GetBytes("Test".PadRight(10, '\0')));
            m.Put(A3Addresses.ManagerLastName, System.Text.Encoding.ASCII.GetBytes("Trainer".PadRight(16, '\0')));
            using (MainWindow window = Window(m))
            {
                window.OnGameAttached(); // manager 3 has the turn
                Assert.Equal("A3-Trainer :: Eigener Kader: Hamburger FC", window.Text);

                var trainer = window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().Single(i => i.Text == "Trainer");
                TestHelpers.Invoke(window, "ListManagers");
                string[] items = trainer.DropDownItems.Cast<ToolStripItem>().Select(i => i.Text).ToArray();
                Assert.Equal(new[] { "Manager", "", "Spieler 1: Test Trainer (1.FV München)", "Spieler 3: Zweiter (Hamburger FC)" }, items);
                Assert.True(((ToolStripMenuItem)trainer.DropDownItems[3]).Checked);

                TestHelpers.Click(trainer.DropDownItems[2]);
                Assert.Equal("A3-Trainer :: Eigener Kader: 1.FV München", window.Text);
                TestHelpers.Click(MenuItem(window, "Verein", "Eigener Verein"));
                Assert.Equal("A3-Trainer :: Verein: 1.FV München", window.Text);
                TestHelpers.Click(trainer.DropDownItems[3]);
                Assert.Equal("A3-Trainer :: Verein: Hamburger FC", window.Text);
                TestHelpers.Click(MenuItem(window, "Trainer", "Manager"));
                Assert.Equal("A3-Trainer :: Manager (Spieler 3)", window.Text);
                TestHelpers.Click(trainer.DropDownItems[2]);
                Assert.Equal("A3-Trainer :: Manager (Spieler 1)", window.Text);

                // A single manager adds no list; the next opening removes the old one.
                m.PutUInt16(A3Addresses.ManagerCount, 1);
                TestHelpers.Invoke(window, "ListManagers");
                Assert.Single(trainer.DropDownItems);
            }
        });

        [Fact]
        public void GameEvents_SwitchBetweenTheLastPageAndTheWaitingScreen() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(WithOpponent()))
            {
                TestHelpers.Click(MenuItem(window, "Trainer", "Manager"));

                window.OnGameLost();
                Assert.IsType<Label>(Shown(window));
                Assert.Equal("A3-Trainer", window.Text);

                window.ShowStatus("Nicht unterstützt");
                Assert.Equal("Nicht unterstützt", Shown(window).Text);

                window.OnGameAttached();
                Assert.Same(TestHelpers.Get<PagesView>(window, "managerPages"), Shown(window));
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

        [Fact]
        public void Layout_ClubChoiceSitsAboveTheTabsLikeOnTheSquadPage_ViewsFitTheirTabs_SquadListNeedsNoHorizontalScrolling() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(ClubSelectionTests.Clubs()))
            {
                TestHelpers.Click(MenuItem(window, "Verein", "Anderer Verein"));
                window.CreateControl();
                foreach (string name in new[] { "clubPages", "managerPages" })
                {
                    TabControl tabs = TestHelpers.Get<TabControl>(TestHelpers.Get<PagesView>(window, name), "tabs");
                    tabs.CreateControl();
                    foreach (TabPage page in tabs.TabPages)
                    {
                        // Every control of the view lies inside its tab page.
                        Control view = page.Controls[0];
                        Assert.All(view.Controls.Cast<Control>(), c => Assert.True(c.Bottom <= tabs.DisplayRectangle.Height && c.Right <= tabs.DisplayRectangle.Width, page.Text + ": " + c.Text));
                    }
                }

                // Same place and size on the club and the squad page, the tabs below.
                PlayerView squad = TestHelpers.Get<PlayerView>(window, "playerView");
                TabControl clubTabs = TestHelpers.Get<TabControl>(TestHelpers.Get<PagesView>(window, "clubPages"), "tabs");
                Assert.Equal(TestHelpers.Get<ComboBox>(squad, "countryBox").Bounds, TestHelpers.Get<ComboBox>(window, "clubCountryBox").Bounds);
                Assert.Equal(TestHelpers.Get<ComboBox>(squad, "clubBox").Bounds, TestHelpers.Get<ComboBox>(window, "clubClubBox").Bounds);
                Assert.True(TestHelpers.Get<ComboBox>(window, "clubClubBox").Bottom < clubTabs.Top);
                Assert.Equal(TestHelpers.Get<ListView>(squad, "list").Top, clubTabs.Top);

                ListView list = TestHelpers.Get<ListView>(TestHelpers.Get<PlayerView>(window, "playerView"), "list");
                int columns = list.Columns.Cast<ColumnHeader>().Sum(c => c.Width);
                Assert.True(columns + SystemInformation.VerticalScrollBarWidth <= list.ClientSize.Width,
                    $"{columns} + scroll bar > {list.ClientSize.Width}");
            }
        });

        [Fact]
        public void History_AltLeftGoesBack_OtherKeysPassOn() => StaThread.Run(() =>
        {
            using (MainWindow window = Window(WithOpponent()))
            {
                window.OnGameAttached();
                TestHelpers.Click(MenuItem(window, "?", "Hilfe"));
                Message msg = default(Message);

                Assert.True((bool)TestHelpers.Invoke(window, "ProcessCmdKey", msg, Keys.Alt | Keys.Left));
                Assert.Equal("A3-Trainer :: Eigener Kader: 1.FV München", window.Text);
                Assert.False((bool)TestHelpers.Invoke(window, "ProcessCmdKey", msg, Keys.F12));
            }
        });

        [Fact]
        public void Mechanics_AFailedInterviewTick_IsLogged_AndTheEffectsStayOn() => StaThread.Run(() =>
        {
            FakeGameMemory m = InterviewEffectsTests.WithInterview(out _);
            using (MainWindow window = Window(m))
            {
                var effects = new InterviewEffects(m, new TransferInjectorTests.FakeCode(m));
                effects.SetEnabled(true);
                TestHelpers.Set(effects, "memory", new BrokenGameMemory());
                TestHelpers.Set(window, "interviewEffects", effects);

                TestHelpers.Invoke(window, "TickMechanics");

                Assert.True(effects.Enabled);
            }
        });
    }
}
