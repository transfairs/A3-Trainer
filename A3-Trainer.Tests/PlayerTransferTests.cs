using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>The player's club in the records, and requesting a transfer on the player view's "Wechsel" tab.</summary>
    public class PlayerTransferTests
    {
        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);

        [Fact]
        public void ReadPlayers_TakesTheClubTheGameKeepsCurrent_NotTheOlderCopy()
        {
            FakeGameMemory m = ClubSelectionTests.Clubs();
            // Moved from Hamburg to München: the bytes at +0x30 still name Hamburg.
            m.Put(Record(1255) + RecordOffset.ClubCountry, 14, 1);
            m.Put(Record(1255) + 0x30, 14, 10);
            var game = new A3Game(m);

            Assert.Contains(game.ReadPlayers(new ClubKey(14, 1)), p => p.Index == 1255);
            Assert.DoesNotContain(new ClubKey(14, 10), game.ReadClubs());
        }

        private static PlayerView View(FakeGameMemory m, TransferInjector transfers)
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            var view = new PlayerView(memory, new A3Game(m)) { Transfers = transfers };
            _ = TestHelpers.Get<ListView>(view, "list").Handle;
            view.ShowClub(new ClubKey(14, 1));
            return view;
        }

        private static void Select(PlayerView view, int index) =>
            TestHelpers.Get<ListView>(view, "list").Items.Cast<ListViewItem>().Single(i => ((Player)i.Tag).Index == index).Selected = true;

        private static string Status(PlayerView view) => TestHelpers.Get<Label>(view, "transferStatus").Text;

        [Fact]
        public void TransferTab_RequestsTheMove_AndReportsTheOutcomeOnce() => StaThread.Run(() =>
        {
            FakeGameMemory m = ClubSelectionTests.Clubs();
            var transfers = new TransferInjector(m, new TransferInjectorTests.FakeCode(m));
            using (PlayerView view = View(m, transfers))
            {
                Select(view, 1041);
                ComboBox country = TestHelpers.Get<ComboBox>(view, "transferCountry");
                ComboBox club = TestHelpers.Get<ComboBox>(view, "transferClub");
                Assert.Equal("Deutschland", country.Text);
                Assert.Equal(new[] { "1.FV München", "Hamburger FC" }, club.Items.Cast<object>().Select(i => i.ToString()));
                Assert.True(TestHelpers.Get<CheckBox>(view, "transferNow").Checked);
                Button request = TestHelpers.Get<Button>(view, "transferRequest");

                TestHelpers.Click(request);
                Assert.Equal("Der Spieler spielt schon bei diesem Verein.", Status(view));
                Assert.Null(transfers.PendingPlayer());

                club.SelectedIndex = 1;
                TestHelpers.Get<NumericUpDown>(view, "transferFee").Value = 500;
                TestHelpers.Get<CheckBox>(view, "transferNow").Checked = false;
                TestHelpers.Click(request);
                Assert.Equal("Wechsel zu Hamburger FC angefordert, er geschieht beim nächsten Tagesabschluss.", Status(view));
                Assert.Equal(1041, transfers.PendingPlayer());
                uint stub = transfers.EnsureInstalled().Value;
                Assert.Equal(new uint[] { 1041, 14, 1, 14, 10, 500000, 0 },
                    Enumerable.Range(0, 7).Select(i => m.ReadUInt32(stub + TransferInjector.ArgumentsOffset + (uint)i * 4)).ToArray());

                // The target stays picked for the next player; the request shows only at its player.
                Select(view, 1039);
                Assert.Equal("Hamburger FC", club.Text);
                Assert.Equal("", Status(view));
                Select(view, 1041);
                Assert.Equal("Wechsel angefordert, er geschieht beim nächsten Tagesabschluss.", Status(view));

                // The daily processing ran the stub.
                m.PutUInt32(stub + TransferInjector.FlagOffset, 0);
                m.PutUInt32(stub + TransferInjector.OutcomeOffset, (uint)TransferInjector.Outcome.Done);
                TestHelpers.Click(TestHelpers.Get<Button>(view, "reload"));
                Assert.Equal("Der letzte angeforderte Wechsel wurde ausgeführt.", Status(view));
                Assert.Equal(TransferInjector.Outcome.None, transfers.TakeOutcome());
            }
        });

        [Fact]
        public void TransferTab_ReportsRefusedMoves_AndWorksWithoutInjector() => StaThread.Run(() =>
        {
            FakeGameMemory m = ClubSelectionTests.Clubs();
            var code = new TransferInjectorTests.FakeCode(m);
            var transfers = new TransferInjector(m, code);
            using (PlayerView view = View(m, transfers))
            {
                Select(view, 1041);
                TestHelpers.Get<ComboBox>(view, "transferClub").SelectedIndex = 1;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "transferRequest"));
                uint stub = transfers.EnsureInstalled().Value;

                foreach (var outcome in new[]
                {
                    (TransferInjector.Outcome.NotInClub, "Der letzte angeforderte Wechsel fiel aus: Der Spieler war nicht mehr beim alten Verein."),
                    (TransferInjector.Outcome.SquadFull, "Der letzte angeforderte Wechsel fiel aus: Der neue Verein hatte schon 35 Spieler."),
                })
                {
                    m.PutUInt32(stub + TransferInjector.FlagOffset, 0);
                    m.PutUInt32(stub + TransferInjector.OutcomeOffset, (uint)outcome.Item1);
                    TestHelpers.Click(TestHelpers.Get<Button>(view, "reload"));
                    Assert.Equal(outcome.Item2, Status(view));
                }

                foreach (uint site in TransferCode.MarketCallSites)
                    m.Put(site, 0x90, 0x90, 0x90, 0x90, 0x90);
                TestHelpers.Click(TestHelpers.Get<Button>(view, "transferRequest"));
                Assert.Equal("Wechsel nicht möglich: Der Code des Spiels ist nicht wie erwartet.", Status(view));

                view.Transfers = null;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "transferRequest"));
                Assert.Equal("Wechsel nicht verfügbar", Status(view));
            }
        });

        [Fact]
        public void TransferTab_TargetClubStartsAtTheFirst_AndRequestingAsksForTheGame() => StaThread.Run(() =>
        {
            FakeGameMemory m = ClubSelectionTests.Clubs();
            var transfers = new TransferInjector(m, new TransferInjectorTests.FakeCode(m));
            using (PlayerView view = View(m, transfers))
            {
                Select(view, 1041);
                ComboBox club = TestHelpers.Get<ComboBox>(view, "transferClub");
                club.SelectedIndex = -1;
                Select(view, 1039);
                Assert.Equal(0, club.SelectedIndex);

                club.SelectedIndex = 1;
                TestHelpers.Get<ProcessMemory>(view, "memory").mProc.Process = null;
                TestHelpers.ExpectDialog("Error", () => TestHelpers.Click(TestHelpers.Get<Button>(view, "transferRequest")));
                Assert.Null(transfers.PendingPlayer());
            }
        });
    }
}
