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
    /// <summary>A human manager's career: titles, stations, years and games as trainer.</summary>
    public class CareerTests
    {
        private const uint Data = A3Addresses.ManagerData;

        // Manager 1: 3 championships, 2 trainer-of-the-month awards, dismissed once, 1 station, 5 years;
        // 12 league games with 7 wins, 3 cup games with 2 wins, 4 European cup games with 1 win.
        internal static FakeGameMemory WithCareer()
        {
            FakeGameMemory m = PersonTests.WithPerson();
            m.Map(A3Addresses.ManagerCareer, A3Addresses.MaxManagers * A3Addresses.ManagerCareerStride);
            m.PutUInt16(A3Addresses.ManagerCareer, 3);
            m.PutUInt16(A3Addresses.ManagerCareer + 0x22, 2);
            m.PutUInt16(A3Addresses.ManagerCareer + A3Addresses.CareerDismissed, 1);
            m.Put(A3Addresses.ManagerCareer + A3Addresses.CareerStations, 1);
            m.PutUInt16(A3Addresses.ManagerYears, 5);
            m.PutUInt32(Data + A3Addresses.ManagerDataLeagueGames, 12);
            m.PutUInt32(Data + A3Addresses.ManagerDataLeagueGames + 4, 7);
            m.PutUInt16(Data + A3Addresses.ManagerDataCupGames, 3);
            m.PutUInt16(Data + A3Addresses.ManagerDataCupGames + 2, 2);
            m.PutUInt16(Data + A3Addresses.ManagerDataCupGames + 4, 4);
            m.PutUInt16(Data + A3Addresses.ManagerDataCupGames + 6, 1);
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        [Fact]
        public void ReadCareer_TakesTitlesStationsYearsAndGames()
        {
            ManagerCareer career = new A3Game(WithCareer()).ReadCareer(1);

            Assert.Equal(16, career.Titles.Length);
            Assert.Equal(3, career.Titles[0]);
            Assert.Equal(2, career.Titles[14]);   // Trainer des Monats
            Assert.Equal(new[] { 1, 0, 1, 5 }, new[] { career.Dismissed, career.Resigned, career.Stations, career.Years });
            Assert.Equal(new[] { 12, 3, 4, 0 }, career.Games);
            Assert.Equal(new[] { 7, 2, 1, 0 }, career.Wins);
            Assert.Equal(0, new A3Game(WithCareer()).ReadCareer(2).Titles.Sum());
            Assert.Equal(0, new A3Game(new FakeGameMemory()).ReadCareer(1).Games[0]);
        }

        [Fact]
        public void SaveCareer_WritesOnlyWhatChanged_YearsToObjectAndData()
        {
            FakeGameMemory m = WithCareer();
            var game = new A3Game(m);
            ManagerCareer original = game.ReadCareer(1);
            game.SaveCareer(original, original.Clone());
            Assert.Empty(m.Writes);

            ManagerCareer edited = original.Clone();
            edited.Titles[9] = 1;       // Weltpokale at +0x14
            edited.Resigned = 2;
            edited.Years = 6;
            edited.Games[0] = 13;
            edited.Wins[3] = 1;
            edited.Games[3] = 2;
            game.SaveCareer(original, edited);

            Assert.Equal(new byte[] { 1, 0 }, m.WrittenAt(A3Addresses.ManagerCareer + 0x14));
            Assert.Equal(new byte[] { 2, 0 }, m.WrittenAt(A3Addresses.ManagerCareer + A3Addresses.CareerResigned));
            Assert.Equal(new byte[] { 6, 0 }, m.WrittenAt(A3Addresses.ManagerYears));
            Assert.Equal(new byte[] { 6, 0 }, m.WrittenAt(Data + 0x144));
            Assert.Equal(BitConverter.GetBytes(13), m.WrittenAt(Data + A3Addresses.ManagerDataLeagueGames));
            Assert.Equal(new byte[] { 2, 0 }, m.WrittenAt(Data + A3Addresses.ManagerDataCupGames + 8));
            Assert.Equal(new byte[] { 1, 0 }, m.WrittenAt(Data + A3Addresses.ManagerDataCupGames + 10));
            Assert.Null(m.WrittenAt(A3Addresses.ManagerCareer));
            Assert.Equal(0, original.Titles[9]);   // the clone is deep
        }

        [Fact]
        public void CareerView_ShowsEditsAndRefusesMoreWinsThanGames() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithCareer();
            using (var view = new CareerView(Attached(), new A3Game(m)) { Manager = 1 })
            {
                view.RefreshValues();
                NumericUpDown[] titles = TestHelpers.Get<NumericUpDown[]>(view, "titles");
                NumericUpDown[] games = TestHelpers.Get<NumericUpDown[]>(view, "games");
                NumericUpDown[] wins = TestHelpers.Get<NumericUpDown[]>(view, "wins");
                Assert.Equal(3, titles[0].Value);
                Assert.Equal("1", TestHelpers.Get<Label>(view, "stations").Text);
                Assert.Equal(12, games[0].Value);

                titles[0].Value = 7;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Equal("Gespeichert", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(new byte[] { 7, 0 }, m.WrittenAt(A3Addresses.ManagerCareer));

                wins[1].Value = 9;          // 3 cup games
                int writes = m.Writes.Count;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Equal("Mehr Siege als Spiele", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(writes, m.Writes.Count);
            }
        });

        [Fact]
        public void CareerView_WithoutGame_AndWhenWritingFails() => StaThread.Run(() =>
        {
            using (var view = new CareerView(new ProcessMemory(), new A3Game(WithCareer())))
            {
                view.RefreshValues();
                Assert.Equal("Kein Spiel verbunden", TestHelpers.Get<Label>(view, "status").Text);
                Assert.False(TestHelpers.Get<Button>(view, "save").Enabled);
                TestHelpers.Invoke(view, "Save");
            }
            using (var view = new CareerView(Attached(), new A3Game(new ReadOnlyGameMemory(WithCareer()))) { Manager = 1 })
            {
                view.RefreshValues();
                TestHelpers.Get<NumericUpDown>(view, "dismissed").Value = 3;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Equal("Fehler beim Speichern", TestHelpers.Get<Label>(view, "status").Text);
            }
        });
    }
}
