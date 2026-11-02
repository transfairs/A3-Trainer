using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>Reading and editing a club's stadium.</summary>
    public class StadiumTests
    {
        // München's stadium as the game has it: seats on the main and opposite tribune, standing in the curves.
        private static readonly uint Munich = StadiumAddress.CityStadiums + StadiumAddress.CityStadiumSize + StadiumAddress.Data;
        // Aarhus IL (13/0, club 220) is past the first club with a CDatenStadion (188).
        private static readonly uint Aarhus = StadiumAddress.OtherStadiums + (220 - 188) * StadiumAddress.OtherStadiumSize + StadiumAddress.Data;

        internal static FakeGameMemory WithStadiums()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.PutUInt16(StadiumAddress.OtherFirst, 188);
            m.Map(Munich, 0xD0);
            m.Put(Munich + StadiumOffset.Name, Encoding.ASCII.GetBytes("Zentralstadion\0"));
            m.Put(Munich + StadiumOffset.City, Encoding.GetEncoding(1252).GetBytes("München\0"));
            m.PutUInt32(Munich + StadiumOffset.Capacity, 69000);
            m.PutUInt32(Munich + StadiumOffset.Capacity2, 69000);
            m.PutUInt16(Munich + StadiumOffset.Seats, 19000);
            m.PutUInt16(Munich + StadiumOffset.Seats + 2, 18000);
            m.PutUInt16(Munich + StadiumOffset.Standing + 4, 16000);
            m.PutUInt16(Munich + StadiumOffset.Standing + 6, 16000);
            m.Put(Munich + StadiumOffset.Condition, 15, 15, 15, 17);
            m.PutUInt16(Munich + StadiumOffset.TribuneBits, 0xCF3);
            m.PutUInt16(Munich + StadiumOffset.Comfort, 0x2E0);
            m.Put(Munich + StadiumOffset.Scoreboard, 3);

            m.PutUInt16(A3Addresses.ClubIndexTable + 13 * 2, 220);
            A3GameTests.PutClub(m, 220, "Aarhus IL");
            m.Map(Aarhus, 0xD0);
            m.Put(Aarhus + StadiumOffset.Name, Encoding.ASCII.GetBytes("Aarhus Stadion\0"));
            m.PutUInt16(Aarhus + StadiumOffset.Seats, 5000);
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        [Fact]
        public void ReadStadium_FromEitherArray_AndNullWithoutOne()
        {
            var game = new A3Game(WithStadiums());

            Stadium munich = game.ReadStadium(new ClubKey(14, 1));
            Assert.Equal("Zentralstadion", munich.Name);
            Assert.Equal("München", munich.City);
            Assert.Equal(new[] { 19000, 18000, 0, 0 }, munich.Seats);
            Assert.Equal(new[] { 0, 0, 16000, 16000 }, munich.Standing);
            Assert.Equal(69000, munich.Capacity);
            Assert.Equal(3, munich.Scoreboard);

            Assert.Equal(5000, game.ReadStadium(new ClubKey(13, 0)).Capacity);
            Assert.Null(game.ReadStadium(new ClubKey(14, 10)));   // Hamburg: no stadium name
            Assert.Null(game.ReadStadium(new ClubKey(14, 2)));    // no club
            Assert.Null(new A3Game(new FakeGameMemory()).ReadStadium(new ClubKey(14, 1)));
        }

        [Fact]
        public void SaveStadium_WritesWhatChanged_AndTheCapacity()
        {
            FakeGameMemory m = WithStadiums();
            var game = new A3Game(m);
            Stadium original = game.ReadStadium(new ClubKey(14, 1));
            Stadium edited = original.Clone();
            edited.Vip[0] = 500;
            edited.Condition[1] = 0;
            edited.Name = "Neues Stadion";
            edited.City = "";                    // an empty city stays

            game.SaveStadium(original, edited);

            Assert.Equal(500, m.GetUInt16(Munich + StadiumOffset.Vip));
            Assert.Equal(0, m.Get(Munich + StadiumOffset.Condition + 1));
            Assert.Equal(69500, (int)m.ReadUInt32(Munich + StadiumOffset.Capacity));
            Assert.Equal(69500, (int)m.ReadUInt32(Munich + StadiumOffset.Capacity2));
            Assert.Equal("Neues Stadion", game.ReadStadium(new ClubKey(14, 1)).Name);
            Assert.Null(m.WrittenAt(Munich + StadiumOffset.City));
            Assert.Null(m.WrittenAt(Munich + StadiumOffset.TribuneBits));
            Assert.Equal(original.Seats, edited.Seats);  // the clone has its own arrays
            Assert.NotSame(original.Condition, edited.Condition);
        }

        [Fact]
        public void View_ShowsAndSavesTheStadium() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithStadiums();
            using (var view = new StadiumView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal("Zentralstadion", TestHelpers.Get<TextBox>(view, "stadiumName").Text);
                Assert.Equal(69000.ToString("N0"), TestHelpers.Get<Label>(view, "capacity").Text);
                Assert.Equal("0", TestHelpers.Get<NumericUpDown[]>(view, "vip")[0].Text);
                CheckBox[] roofs = TestHelpers.Get<CheckBox[]>(view, "roofs");
                CheckBox[] heaters = TestHelpers.Get<CheckBox[]>(view, "heaters");
                Assert.Equal(new[] { true, true, false, false }, roofs.Select(c => c.Checked));
                Assert.Equal(new[] { false, false, true, true }, heaters.Select(c => c.Checked));
                Assert.True(TestHelpers.Get<CheckBox[]>(view, "tracks").All(c => c.Checked));
                ComboBox[] conditions = TestHelpers.Get<ComboBox[]>(view, "conditions");
                Assert.Equal("angestaubt", conditions[3].Text);   // 17: level 3
                Assert.True(TestHelpers.Get<CheckBox>(view, "floodlight").Checked);
                Assert.False(TestHelpers.Get<CheckBox>(view, "luxuryBoxes").Checked);
                Assert.Equal("große LED-Anzeigetafel", TestHelpers.Get<ComboBox>(view, "scoreboard").Text);

                TestHelpers.Get<NumericUpDown[]>(view, "vip")[0].Value = 500;
                Assert.Equal(69500.ToString("N0"), TestHelpers.Get<Label>(view, "capacity").Text);
                roofs[3].Checked = true;
                heaters[0].Checked = true;
                conditions[0].SelectedIndex = 0;
                TestHelpers.Get<CheckBox>(view, "luxuryBoxes").Checked = true;
                TestHelpers.Get<CheckBox>(view, "floodlight").Checked = false;
                TestHelpers.Get<ComboBox>(view, "scoreboard").SelectedIndex = 4;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));

                Assert.Equal("Gespeichert", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(0xCF3 | 0x8 | 0x100, m.GetUInt16(Munich + StadiumOffset.TribuneBits));
                Assert.Equal(0x2E0 & ~StadiumComfort.Floodlight | StadiumComfort.LuxuryBoxes, m.GetUInt16(Munich + StadiumOffset.Comfort));
                Assert.Equal(new byte[] { 0, 15, 15, 17 }, m.Read(Munich + StadiumOffset.Condition, 4));  // untouched levels keep their value
                Assert.Equal(4, m.Get(Munich + StadiumOffset.Scoreboard));
                Assert.Equal(500, m.GetUInt16(Munich + StadiumOffset.Vip));
            }
        });

        [Fact]
        public void View_ShowsEachTribunesHeater() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithStadiums();
            m.PutUInt16(Munich + StadiumOffset.TribuneBits, 0xEF3);
            using (var view = new StadiumView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal(new[] { false, true, true, true }, TestHelpers.Get<CheckBox[]>(view, "heaters").Select(c => c.Checked));
            }
        });

        [Fact]
        public void View_WithoutAStadium_OrAWriteError() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithStadiums();
            m.Put(Munich + StadiumOffset.Condition, 99, 0, 0, 0);    // no level of the list
            m.Put(Munich + StadiumOffset.Scoreboard, 9);
            using (var view = new StadiumView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal(-1, TestHelpers.Get<ComboBox[]>(view, "conditions")[0].SelectedIndex);
                Assert.Equal(-1, TestHelpers.Get<ComboBox>(view, "scoreboard").SelectedIndex);
                TestHelpers.Get<TextBox>(view, "stadiumName").Text = " ";
                TestHelpers.Set(view, "game", new A3Game(new ReadOnlyGameMemory(m)));
                TestHelpers.Get<NumericUpDown[]>(view, "seats")[0].Value = 1;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Equal("Fehler beim Speichern", TestHelpers.Get<Label>(view, "status").Text);

                view.Club = new ClubKey(14, 10);
                view.RefreshValues();
                Assert.Equal("Kein Stadion gefunden", TestHelpers.Get<Label>(view, "status").Text);
                Assert.False(TestHelpers.Get<Button>(view, "save").Enabled);
                Assert.Equal("", TestHelpers.Get<Label>(view, "capacity").Text);
            }
        });

        [Fact]
        public void Window_ClubMenu_ShowsTheStadium() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithStadiums();
            using (var window = new MainWindow())
            {
                var game = new A3Game(m);
                TestHelpers.Set(window, "game", game);
                TestHelpers.Set(TestHelpers.Get<StadiumView>(window, "stadiumView"), "game", game);
                TestHelpers.Get<ProcessMemory>(window, "memory").mProc.Process = Process.GetCurrentProcess();
                ToolStripItem stadium = window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().Single(i => i.Text == "Verein")
                    .DropDownItems.Cast<ToolStripItem>().Single(i => i.Text == "Stadion");

                TestHelpers.Click(stadium);

                Assert.Equal("A3-Trainer :: Stadion: 1.FV München", window.Text);
                Assert.Equal("Zentralstadion", TestHelpers.Get<TextBox>(TestHelpers.Get<StadiumView>(window, "stadiumView"), "stadiumName").Text);
            }
        });
    }
}
