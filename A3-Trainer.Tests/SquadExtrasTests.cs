using A3_Trainer.Controller;
using A3_Trainer.Model;
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
    /// <summary>Youth players, the own club's amateur team and the transfer list.</summary>
    public class SquadExtrasTests
    {
        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);
        private static uint Club(int index) => A3Addresses.ClubArray + (uint)(index * A3Addresses.ClubSize);
        private static uint Entry(int position) =>
            A3Addresses.TransferList + A3Addresses.TransferListEntries + (uint)(position * A3Addresses.TransferListEntrySize);

        // München (14/1) with Huhn (1039), the A-Jugend player 352, and its amateur team 14/40 with player 1988.
        // The transfer list holds players 1015 and 1168.
        internal static FakeGameMemory WithSquadExtras()
        {
            FakeGameMemory m = A3GameTests.Career();
            A3GameTests.PutPlayer(m, 1039, 14, 1, 1, 11);
            A3GameTests.PutPlayer(m, 352, 14, 1, 3, 3);
            m.Put(Record(352) + RecordOffset.YouthTeam, 11);
            A3GameTests.PutClub(m, 40, "1.FV München (A)");
            A3GameTests.PutPlayer(m, 1988, 14, 40, 1, 9);
            m.Put(Club(1) + A3Addresses.ClubAmateurs, 40);
            m.Put(Club(10) + A3Addresses.ClubAmateurs, 0xFF);
            m.Map(A3Addresses.TransferList, A3Addresses.TransferListEntries + A3Addresses.TransferListMax * A3Addresses.TransferListEntrySize);
            m.PutUInt16(A3Addresses.TransferList + A3Addresses.TransferListCount, 2);
            m.PutUInt16(Entry(0), 1015);
            m.PutUInt16(Entry(1), 1168);
            m.Put(Entry(1) + 7, 8);
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        [Theory]
        [InlineData(0, "1. Mannschaft")]
        [InlineData(11, "A-Jugend")]
        [InlineData(10, "B-Jugend")]
        [InlineData(9, "C-Jugend")]
        [InlineData(7, "Jugend (7)")]
        public void Team_NamesTheYouthTeams(int youthTeam, string name) => Assert.Equal(name, Tables.Team(youthTeam));

        [Fact]
        public void Players_CarryTheirYouthTeam()
        {
            var players = new A3Game(WithSquadExtras()).ReadPlayers(new ClubKey(14, 1));
            Player youth = players.Single(p => p.Index == 352);
            Assert.True(youth.IsYouth);
            Assert.Equal(11, youth.YouthTeam);
            Assert.False(players.Single(p => p.Index == 1039).IsYouth);
        }

        [Fact]
        public void AmateurClub_ComesFromTheRecord()
        {
            var game = new A3Game(WithSquadExtras());
            Assert.Equal(new ClubKey(14, 40), game.AmateurClub(new ClubKey(14, 1)));
            Assert.Null(game.AmateurClub(new ClubKey(14, 10)));   // none
            Assert.Null(game.AmateurClub(new ClubKey(14, 50)));   // no such club
            FakeGameMemory m = WithSquadExtras();
            m.Put(Club(1) + A3Addresses.ClubAmateurs, 77);        // points at no club
            Assert.Null(new A3Game(m).AmateurClub(new ClubKey(14, 1)));
        }

        [Fact]
        public void TransferList_ListsAddsAndRemovesPlayers()
        {
            FakeGameMemory m = WithSquadExtras();
            m.PutUInt16(A3Addresses.DayCounter, 12);
            var game = new A3Game(m);
            Assert.Equal(new[] { 1015, 1168 }, game.TransferListPlayers());

            Player huhn = game.ReadPlayers(new ClubKey(14, 1)).Single(p => p.Index == 1039);
            Player listed = Clone(huhn);
            listed.NationalTeam |= StatusFlag.OnTransferList;
            game.SavePlayer(huhn, listed);
            Assert.Equal(new[] { 1015, 1168, 1039 }, game.TransferListPlayers());
            byte[] entry = m.Read(Entry(2), A3Addresses.TransferListEntrySize);
            Assert.Equal(new byte[] { 0x0F, 0x04, 14, 0, 1, 0, 1, 11, 11, 0, 12, 0, 1, 3, 100, 0 }, entry.Take(16).ToArray());
            Assert.Equal(StatusFlag.OnTransferList, m.Read(Record(1039) + RecordOffset.NationalTeam, 1)[0] & StatusFlag.OnTransferList);

            // Saving again changes nothing on the list.
            game.SavePlayer(listed, Clone(listed));
            Assert.Equal(3, game.TransferListPlayers().Count);

            // Taking a player off closes the gap.
            Player first = Clone(huhn);
            first.Index = 1015;
            first.NationalTeam = StatusFlag.OnTransferList;
            Player off = Clone(first);
            off.NationalTeam = 0;
            game.SavePlayer(first, off);
            Assert.Equal(new[] { 1168, 1039 }, game.TransferListPlayers());
            Assert.Equal(8, m.Read(Entry(0) + 7, 1)[0]);

            // A player who isn't on it stays off; a full list takes nobody.
            game.SavePlayer(first, off);
            Assert.Equal(2, game.TransferListPlayers().Count);
            m.PutUInt16(A3Addresses.TransferList + A3Addresses.TransferListCount, A3Addresses.TransferListMax);
            Player other = Clone(huhn);
            other.Index = 2000;
            Player otherListed = Clone(other);
            otherListed.NationalTeam = StatusFlag.OnTransferList;
            game.SavePlayer(other, otherListed);
            Assert.DoesNotContain(2000, game.TransferListPlayers());

            Assert.Empty(new A3Game(A3GameTests.Career()).TransferListPlayers());
        }

        private static Player Clone(Player player) => (Player)typeof(Player).GetMethod("MemberwiseClone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(player, null);

        [Fact]
        public void PlayerView_MarksYouthPlayers_ShowsTheTeam_AndListsForTransfer() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithSquadExtras();
            using (var view = new PlayerView(Attached(), new A3Game(m)))
            {
                ListView list = TestHelpers.Get<ListView>(view, "list");
                _ = list.Handle;
                view.ShowClub(new ClubKey(14, 1));
                ListViewItem youth = list.Items.Cast<ListViewItem>().Single(i => ((Player)i.Tag).Index == 352);
                Assert.Equal(PlayerView.YouthBackColor, youth.BackColor);

                youth.Selected = true;
                Assert.Equal("A-Jugend", TestHelpers.Get<Label>(view, "teamValue").Text);
                CheckBox listed = TestHelpers.Get<CheckBox>(view, "transferListed");
                Assert.False(listed.Checked);
                listed.Checked = true;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Contains(352, new A3Game(m).TransferListPlayers());

                view.ShowClub(null);
                Assert.Equal("", TestHelpers.Get<Label>(view, "teamValue").Text);
            }
        });

        [Fact]
        public void Window_AmateursMenu_ShowsTheOwnAmateurTeam() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithSquadExtras();
            using (var window = new MainWindow())
            {
                var game = new A3Game(m);
                TestHelpers.Set(window, "game", game);
                foreach (string view in new[] { "playerView", "clubView", "identityView", "stadiumView", "boardView", "financesView", "managerView", "personView", "stocksView" })
                    TestHelpers.Set(TestHelpers.Get<object>(window, view), "game", game);
                TestHelpers.Get<ProcessMemory>(window, "memory").mProc.Process = Process.GetCurrentProcess();
                ToolStripItem amateurs = window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().Single(i => i.Text == "Spieler")
                    .DropDownItems.Cast<ToolStripItem>().Single(i => i.Text == "Amateure");

                TestHelpers.Click(amateurs);
                Assert.Equal("A3-Trainer :: Amateure: 1.FV München (A)", window.Text);

                m.Put(Club(1) + A3Addresses.ClubAmateurs, 0xFF);
                TestHelpers.Click(amateurs);
                Assert.Equal("A3-Trainer :: Amateure (keine Amateurmannschaft)", window.Text);
            }
        });
    }
}
