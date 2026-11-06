using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>Reading and editing a club's president, opposition leader, founding date and eternal table.</summary>
    public class BoardTests
    {
        private static readonly uint Munich = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;

        // München (club 1): president Oliver Huhn, born 23.3.1941; the opposition leader has no names yet;
        // founded 6.8.1900; eternal table 2506:1428 goals in 1160 games, 2185 points.
        internal static FakeGameMemory WithBoard()
        {
            FakeGameMemory m = A3GameTests.WithNamePool(A3GameTests.Career());
            m.Put(Munich + A3Addresses.ClubPresident, 1, 0, 2, 0, 23, 3, 0x95, 0x07);
            m.Put(Munich + A3Addresses.ClubOpposition, 0, 0, 0, 0, 8, 8, 0xA0, 0x07);
            m.Put(Munich + A3Addresses.ClubFounded, 6, 8, 0x6C, 0x07);
            m.PutUInt32(Munich + A3Addresses.ClubEternalGoals, 2506);
            m.PutUInt32(Munich + A3Addresses.ClubEternalConceded, 1428);
            m.PutUInt32(Munich + A3Addresses.ClubEternalGames, 1160);
            m.PutUInt32(Munich + A3Addresses.ClubEternalPoints, 2185);
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        private static T Field<T>(object owner, string name) => TestHelpers.Get<T>(owner, name);

        private static NumericUpDown Part(BoardView view, string member, string date, string part)
        {
            object inputs = member == null ? Field<object>(view, date) : Field<object>(Field<object>(view, member), date);
            return Field<NumericUpDown>(inputs, part);
        }

        [Fact]
        public void ReadBoard_TakesNamesDatesAndTheEternalTableFromTheRecord()
        {
            ClubBoard board = new A3Game(WithBoard()).ReadBoard(new ClubKey(14, 1));

            Assert.Equal("Oliver", board.President.FirstName);
            Assert.Equal("Huhn", board.President.LastName);
            Assert.Equal(new GameDate(1941, 3, 23), board.President.Birthday);
            Assert.Equal("", board.Opposition.FirstName);
            Assert.Equal(new GameDate(1952, 8, 8), board.Opposition.Birthday);
            Assert.Equal(new GameDate(1900, 8, 6), board.Founded);
            Assert.Equal(new[] { 2506, 1428, 1160, 2185 }, new[] { board.Goals, board.Conceded, board.Games, board.Points });

            Assert.Null(new A3Game(WithBoard()).ReadBoard(new ClubKey(14, 50)));
        }

        [Fact]
        public void SaveBoard_WritesOnlyWhatChanged_AndAddsNewNames()
        {
            FakeGameMemory m = WithBoard();
            var game = new A3Game(m);
            ClubBoard original = game.ReadBoard(new ClubKey(14, 1));
            ClubBoard edited = original.Clone();
            edited.Opposition.FirstName = "Huhn";          // already in the name table
            edited.Opposition.LastName = "Neuer";          // added
            edited.President.Birthday = new GameDate(1945, 1, 11);
            edited.Points = 2190;

            game.SaveBoard(original, edited);

            Assert.Equal(new byte[] { 2, 0 }, m.WrittenAt(Munich + A3Addresses.ClubOpposition));
            Assert.Equal("Neuer", game.ReadName(m.GetUInt16(Munich + A3Addresses.ClubOpposition + 2)));
            Assert.Equal(new byte[] { 11, 1, 0x99, 0x07 }, m.WrittenAt(Munich + A3Addresses.ClubPresident + 4));
            Assert.Null(m.WrittenAt(Munich + A3Addresses.ClubPresident));
            Assert.Null(m.WrittenAt(Munich + A3Addresses.ClubFounded));
            Assert.Equal(BitConverter.GetBytes(2190), m.WrittenAt(Munich + A3Addresses.ClubEternalPoints));
            Assert.Null(m.WrittenAt(Munich + A3Addresses.ClubEternalGoals));
            Assert.Equal("Oliver", original.President.FirstName); // the clone is deep
        }

        [Fact]
        public void BoardView_ShowsEditsAndSaves() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithBoard();
            using (var view = new BoardView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                object president = Field<object>(view, "president");
                Assert.Equal("Oliver", Field<TextBox>(president, "firstName").Text);
                Assert.Equal(58, int.Parse(Field<Label>(president, "age").Text)); // on 4.7.1999
                Assert.Equal(1900, Part(view, null, "founded", "Year").Value);
                Assert.Equal(2185, Field<NumericUpDown>(view, "points").Value);

                Field<TextBox>(president, "firstName").Text = " ";  // an emptied name stays
                Part(view, null, "founded", "Year").Value = 1899;
                Field<NumericUpDown>(view, "games").Value = 1200;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Gespeichert", Field<Label>(view, "status").Text);
                Assert.Null(m.WrittenAt(Munich + A3Addresses.ClubPresident));
                Assert.Equal(new byte[] { 6, 8, 0x6B, 0x07 }, m.WrittenAt(Munich + A3Addresses.ClubFounded));
                Assert.Equal(BitConverter.GetBytes(1200), m.WrittenAt(Munich + A3Addresses.ClubEternalGames));
            }
        });

        [Fact]
        public void BoardView_RefusesADateTheCalendarDoesntHave() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithBoard();
            using (var view = new BoardView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Part(view, "opposition", "birthday", "Day").Value = 31;
                Part(view, "opposition", "birthday", "Month").Value = 2;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Ungültiges Datum", Field<Label>(view, "status").Text);
                Assert.Empty(m.Writes);
            }
        });

        [Fact]
        public void BoardView_WithoutAClubOrGame_ShowsNothing() => StaThread.Run(() =>
        {
            using (var view = new BoardView(new ProcessMemory(), new A3Game(WithBoard())) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.False(Field<Button>(view, "save").Enabled);
                Assert.Equal("Kein Verein gefunden", Field<Label>(view, "status").Text);
                TestHelpers.Click(Field<Button>(view, "save"));
            }
            using (var view = new BoardView(Attached(), new A3Game(WithBoard())) { Club = new ClubKey(14, 50) })
            {
                view.RefreshValues();
                Assert.Equal("", Field<Label>(Field<object>(view, "president"), "age").Text);
            }
        });

        [Fact]
        public void BoardView_ReportsAFailedSave() => StaThread.Run(() =>
        {
            using (var view = new BoardView(Attached(), new A3Game(new ReadOnlyGameMemory(WithBoard()))) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Field<NumericUpDown>(view, "points").Value = 1;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Fehler beim Speichern", Field<Label>(view, "status").Text);
            }
        });
    }
}
