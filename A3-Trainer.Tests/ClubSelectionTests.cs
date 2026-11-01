using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>Picking any club in the player view, and setting or freezing the whole team's condition and freshness.</summary>
    public class ClubSelectionTests
    {
        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);
        private static uint Compact(int cacheSlot) => 0x06000000 + (uint)(cacheSlot * 0x100) + A3Addresses.CacheEntryCompact;

        // Two German clubs (14/1, 14/10), a Danish one (13/0, club 220) and a player whose club key
        // names no club; 1039 has a working copy. Countries 0-14 have names.
        private static FakeGameMemory Clubs()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.PutUInt16(A3Addresses.ClubIndexTable + 13 * 2, 220);
            A3GameTests.PutClub(m, 220, "Aarhus IL");
            A3GameTests.PutPlayer(m, 1255, 14, 10, 1, 10);
            A3GameTests.PutPlayer(m, 1039, 14, 1, 1, 11);
            A3GameTests.PutPlayer(m, 1041, 14, 1, 2, 12);
            A3GameTests.PutPlayer(m, 9590, 13, 0, 1, 9);
            A3GameTests.PutPlayer(m, 9700, 14, 50, 1, 9);
            A3GameTests.PutCache(m, 1039);

            m.Map(A3Addresses.CountryNames, A3Addresses.CountrySlots * 4);
            for (int country = 0; country <= 14; country++)
            {
                string name = country == 13 ? "Dänemark" : country == 14 ? "Deutschland" : "Land " + country;
                uint text = 0x07000000 + (uint)(country * 0x20);
                m.PutUInt32(A3Addresses.CountryNames + (uint)(country * 4), text);
                m.Put(text, Encoding.GetEncoding(1252).GetBytes(name.PadRight(31, '\0') + "\0"));
            }
            return m;
        }

        private static PlayerView View(FakeGameMemory m)
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            var view = new PlayerView(memory, new A3Game(m));
            _ = TestHelpers.Get<ListView>(view, "list").Handle;
            return view;
        }

        private static string[] Texts(ComboBox box) => box.Items.Cast<object>().Select(i => i.ToString()).ToArray();

        private static int[] Listed(PlayerView view) =>
            TestHelpers.Get<ListView>(view, "list").Items.Cast<ListViewItem>().Select(i => ((Player)i.Tag).Index).ToArray();

        private static int Condition(FakeGameMemory m, int player) => m.GetUInt16(Record(player) + RecordOffset.Condition);
        private static int Freshness(FakeGameMemory m, int player) => m.GetUInt16(Record(player) + RecordOffset.Freshness);

        [Fact]
        public void ReadClubs_ListsEveryClubWithPlayers_InTheGamesOrder()
        {
            List<ClubKey> clubs = new A3Game(Clubs()).ReadClubs();

            Assert.Equal(new[] { new ClubKey(14, 1), new ClubKey(14, 10), new ClubKey(13, 0) }, clubs);
            Assert.Empty(new A3Game(new FakeGameMemory()).ReadClubs());
        }

        [Fact]
        public void SetTeamFitness_WritesRecordAndWorkingCopy_OfPlayersThatDiffer()
        {
            FakeGameMemory m = Clubs();
            var game = new A3Game(m);

            Assert.Equal(2, game.SetTeamFitness(new ClubKey(14, 1), 7700, null));
            Assert.Equal(7700, Condition(m, 1039));
            Assert.Equal(7700, Condition(m, 1041));
            Assert.Equal(7700, m.GetUInt16(Compact(0) + CompactOffset.Condition));
            Assert.Equal(6020, Freshness(m, 1041));
            Assert.Equal(5509, Condition(m, 1255)); // other club untouched

            Assert.Equal(0, game.SetTeamFitness(new ClubKey(14, 1), 7700, null));
            Assert.Equal(1, game.SetTeamFitness(new ClubKey(14, 10), null, 9000));
            Assert.Equal(9000, Freshness(m, 1255));
            Assert.Equal(5509, Condition(m, 1255));
        }

        [Fact]
        public void ClubSelection_ShowsCountriesAlphabetically_AndTheClubsOfTheShownOnesCountry() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Clubs()))
            {
                view.ShowClub(new ClubKey(14, 10));

                ComboBox countries = TestHelpers.Get<ComboBox>(view, "countryBox");
                ComboBox clubs = TestHelpers.Get<ComboBox>(view, "clubBox");
                Assert.Equal(new[] { "Dänemark", "Deutschland" }, Texts(countries));
                Assert.Equal("Deutschland", countries.Text);
                Assert.Equal(new[] { "1.FV München", "Hamburger FC" }, Texts(clubs));
                Assert.Equal("Hamburger FC", clubs.Text);
            }
        });

        [Fact]
        public void PickingACountryOrClub_ShowsItsPlayers_AndTellsTheWindow() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Clubs()))
            {
                var chosen = new List<ClubKey>();
                view.ClubChosen += chosen.Add;
                view.ShowClub(new ClubKey(14, 1));
                Assert.Empty(chosen);

                ComboBox countries = TestHelpers.Get<ComboBox>(view, "countryBox");
                ComboBox clubs = TestHelpers.Get<ComboBox>(view, "clubBox");
                countries.SelectedIndex = 0; // Dänemark: its first club
                Assert.Equal(new[] { "Aarhus IL" }, Texts(clubs));
                Assert.Equal(new[] { 9590 }, Listed(view));

                countries.SelectedIndex = 1;
                Assert.Equal(new[] { 1039, 1041 }, Listed(view));
                clubs.SelectedIndex = 1;
                Assert.Equal(new[] { 1255 }, Listed(view));
                clubs.SelectedIndex = 1; // no change, no event

                Assert.Equal(new[] { new ClubKey(13, 0), new ClubKey(14, 1), new ClubKey(14, 10) }, chosen);
            }
        });

        [Fact]
        public void ClubSelection_StaysEmpty_WithoutAGame() => StaThread.Run(() =>
        {
            var memory = new ProcessMemory();
            using (var view = new PlayerView(memory, new A3Game(Clubs())))
            {
                view.ShowClub(new ClubKey(14, 1));
                Assert.Empty(TestHelpers.Get<ComboBox>(view, "countryBox").Items);

                // Country numbers without a name show as the number.
                memory.mProc.Process = Process.GetCurrentProcess();
                FakeGameMemory unnamed = Clubs();
                unnamed.PutUInt32(A3Addresses.CountryNames + 13 * 4, 0);
                TestHelpers.Set(view, "game", new A3Game(unnamed));
                view.ShowClub(new ClubKey(14, 10));
                Assert.Equal(new[] { "13", "14" }, Texts(TestHelpers.Get<ComboBox>(view, "countryBox")));
            }
        });

        [Fact]
        public void SetForAll_WritesTheWholeTeam_AndReportsTheCount() => StaThread.Run(() =>
        {
            FakeGameMemory m = Clubs();
            using (PlayerView view = View(m))
            {
                view.ShowClub(new ClubKey(14, 1));
                TestHelpers.Get<NumericUpDown>(view, "teamCondition").Value = 80;
                TestHelpers.Get<NumericUpDown>(view, "teamFreshness").Value = 90;

                TestHelpers.Click(TestHelpers.Get<Button>(view, "teamApply"));

                Assert.Equal("2 Spieler geändert", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(8000, Condition(m, 1041));
                Assert.Equal(9000, Freshness(m, 1041));
                Assert.Equal(8000, m.GetUInt16(Compact(0) + CompactOffset.Condition));
            }
        });

        [Fact]
        public void SetForAll_ReportsAWriteError() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Clubs()))
            {
                view.ShowClub(new ClubKey(14, 1));
                TestHelpers.Set(view, "game", new A3Game(new ReadOnlyGameMemory(Clubs())));

                TestHelpers.Click(TestHelpers.Get<Button>(view, "teamApply"));

                Assert.Equal("Fehler beim Speichern", TestHelpers.Get<Label>(view, "status").Text);
            }
        });

        [Fact]
        public void Freezing_KeepsWritingTheFrozenValue_ToTheClubItStartedOn() => StaThread.Run(() =>
        {
            FakeGameMemory m = Clubs();
            using (PlayerView view = View(m))
            {
                view.ShowClub(new ClubKey(14, 1));
                TestHelpers.Get<NumericUpDown>(view, "teamCondition").Value = 77;
                CheckBox freeze = TestHelpers.Get<CheckBox>(view, "freezeCondition");
                Timer timer = TestHelpers.Get<Timer>(view, "freezeTimer");

                freeze.Checked = true;
                Assert.True(timer.Enabled);
                Assert.Equal(7700, Condition(m, 1039));
                Assert.Equal(6020, Freshness(m, 1041)); // freshness isn't frozen

                // The game changes the value; the next tick puts it back.
                m.PutUInt16(Compact(0) + CompactOffset.Condition, 1234);
                m.PutUInt16(Record(1039) + RecordOffset.Condition, 1234);
                TestHelpers.Invoke(view, "Freeze");
                Assert.Equal(7700, Condition(m, 1039));
                Assert.Equal(7700, m.GetUInt16(Compact(0) + CompactOffset.Condition));

                // Also freezing freshness keeps the club it started on.
                TestHelpers.Get<NumericUpDown>(view, "teamFreshness").Value = 50;
                TestHelpers.Get<CheckBox>(view, "freezeFreshness").Checked = true;
                Assert.Equal(5000, Freshness(m, 1041));

                freeze.Checked = false;
                Assert.True(timer.Enabled);
                TestHelpers.Get<CheckBox>(view, "freezeFreshness").Checked = false;
                Assert.False(timer.Enabled);
            }
        });

        [Fact]
        public void Freezing_EndsWhenAnotherClubIsShown_OrTheGameIsGone() => StaThread.Run(() =>
        {
            FakeGameMemory m = Clubs();
            using (PlayerView view = View(m))
            {
                CheckBox freeze = TestHelpers.Get<CheckBox>(view, "freezeCondition");
                view.ShowClub(new ClubKey(14, 1));
                freeze.Checked = true;
                view.ShowClub(new ClubKey(14, 1)); // reload keeps it
                Assert.True(freeze.Checked);

                view.ShowClub(new ClubKey(14, 10));
                Assert.False(freeze.Checked);
                Assert.False(TestHelpers.Get<Timer>(view, "freezeTimer").Enabled);

                freeze.Checked = true;
                TestHelpers.Get<ComboBox>(view, "clubBox").SelectedIndex = 0;
                Assert.False(freeze.Checked);

                freeze.Checked = true;
                view.StopFreezing();
                Assert.False(freeze.Checked);
            }
        });

        [Fact]
        public void Freezing_DoesNothingWithoutAGame_AndLogsErrors() => StaThread.Run(() =>
        {
            FakeGameMemory m = Clubs();
            using (PlayerView view = View(m))
            {
                view.ShowClub(new ClubKey(14, 1));
                TestHelpers.Get<CheckBox>(view, "freezeCondition").Checked = true;

                TestHelpers.Set(view, "game", new A3Game(new ReadOnlyGameMemory(Clubs())));
                m.PutUInt16(Record(1041) + RecordOffset.Condition, 1234);
                TestHelpers.Invoke(view, "Freeze"); // write fails, logged

                TestHelpers.Get<ProcessMemory>(view, "memory").mProc.Process = null;
                TestHelpers.Invoke(view, "Freeze");
                Assert.Equal(1234, Condition(m, 1041));
            }
        });

        [Fact]
        public void Window_OtherClubPage_FollowsThePickedClub() => StaThread.Run(() =>
        {
            FakeGameMemory m = Clubs();
            using (var window = new MainWindow())
            {
                var game = new A3Game(m);
                TestHelpers.Set(window, "game", game);
                PlayerView view = TestHelpers.Get<PlayerView>(window, "playerView");
                TestHelpers.Set(view, "game", game);
                TestHelpers.Get<ProcessMemory>(window, "memory").mProc.Process = Process.GetCurrentProcess();
                ToolStripItem other = window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().Single(i => i.Text == "Spieler")
                    .DropDownItems.Cast<ToolStripItem>().Single(i => i.Text == "Anderer Verein");

                // Nothing picked yet: the own club.
                TestHelpers.Click(other);
                Assert.Equal("A3-Trainer :: Verein: 1.FV München", window.Text);

                TestHelpers.Get<ComboBox>(view, "countryBox").SelectedIndex = 0;
                Assert.Equal("A3-Trainer :: Verein: Aarhus IL", window.Text);

                window.OnGameLost();
                window.OnGameAttached();
                Assert.Equal("A3-Trainer :: Verein: Aarhus IL", window.Text);
                Assert.Equal(new[] { 9590 }, Listed(view));

                TestHelpers.Get<CheckBox>(view, "freezeCondition").Checked = true;
                window.OnGameLost();
                Assert.False(TestHelpers.Get<CheckBox>(view, "freezeCondition").Checked);
            }
        });
    }
}
