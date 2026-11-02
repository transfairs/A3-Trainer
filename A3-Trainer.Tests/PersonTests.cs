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
    /// <summary>Reading and editing a manager's birthday, level and competencies.</summary>
    public class PersonTests
    {
        private const uint Birthday = A3Addresses.ManagerData + A3Addresses.ManagerDataBirthday;
        private const uint DataTable = A3Addresses.ManagerData + 0x54;

        private static uint Cell(int level, int competency) =>
            A3Addresses.ManagerCompetencies + (uint)(level * A3Addresses.ManagerCompetencyRow + competency * 2);

        // Born 1.1.1970, nerves 98, level 9; every competency of a row holds the row's level, as the game starts them.
        internal static FakeGameMemory WithPerson()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.Map(A3Addresses.ManagerData, A3Addresses.ManagerDataStride);
            m.Put(Birthday, 1, 1, 0xB2, 0x07);
            m.PutUInt16(A3Addresses.ManagerNerves, 98);
            m.Put(A3Addresses.ManagerLevel, 9);
            m.Put(A3Addresses.ManagerLevelFlags, 0);
            m.Map(A3Addresses.ManagerCompetencies, A3Addresses.ManagerLevels * A3Addresses.ManagerCompetencyRow);
            for (int level = 0; level < A3Addresses.ManagerLevels; level++)
                for (int c = 0; c < Tables.Competencies.Length; c++)
                    m.PutUInt16(Cell(level, c), level);
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        [Fact]
        public void ReadPerson_AndNullWithoutTheData()
        {
            ManagerPerson person = new A3Game(WithPerson()).ReadPerson(1);
            Assert.Equal(new GameDate(1970, 1, 1), person.Birthday);
            Assert.Equal(98, person.Nerves);
            Assert.Equal(9, person.Level);
            Assert.Equal(9, person.LevelInForce(person.Level));
            Assert.Equal(Enumerable.Repeat(9, 7), person.Competencies[9]);
            Assert.Equal(16, person.Competencies.Length);

            Assert.Null(new A3Game(A3GameTests.Career()).ReadPerson(1));
        }

        [Fact]
        public void SavePerson_WritesObjectAndData_AndTheClonesTableIsItsOwn()
        {
            FakeGameMemory m = WithPerson();
            var game = new A3Game(m);
            ManagerPerson original = game.ReadPerson(1);
            ManagerPerson edited = original.Clone();
            edited.Birthday = new GameDate(1965, 12, 24);
            edited.Nerves = 40;
            edited.Level = 10;
            edited.Competencies[10][0] = 14;

            game.SavePerson(original, edited);

            Assert.Equal(new byte[] { 24, 12, 0xAD, 0x07 }, m.Read(Birthday, 4));
            Assert.Equal(40, m.GetUInt16(A3Addresses.ManagerNerves));
            Assert.Equal(40, m.GetUInt16(A3Addresses.ManagerData + 0x13E));
            Assert.Equal(10, m.Get(A3Addresses.ManagerLevel));
            Assert.Equal(10, m.Get(A3Addresses.ManagerData + 0x43));
            Assert.Equal(14, m.GetUInt16(Cell(10, 0)));
            Assert.Equal(14, m.GetUInt16(DataTable + 10 * A3Addresses.ManagerCompetencyRow));
            Assert.Null(m.WrittenAt(Cell(10, 1)));
            Assert.Equal(10, original.Competencies[10][0]);
        }

        [Fact]
        public void View_ShowsTheRowOfTheLevelInForce_AndSaves() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithPerson();
            m.PutUInt16(Cell(11, 5), 13);
            using (var view = new PersonView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal("1", TestHelpers.Get<NumericUpDown>(view, "birthDay").Text);
                Assert.Equal("29", TestHelpers.Get<Label>(view, "age").Text);
                Assert.Equal(98, TestHelpers.Get<NumericUpDown>(view, "nerves").Value);
                NumericUpDown level = TestHelpers.Get<NumericUpDown>(view, "level");
                NumericUpDown[] competencies = TestHelpers.Get<NumericUpDown[]>(view, "competencies");
                Assert.Equal(9, level.Value);
                Assert.Equal("Fähigkeiten auf Level 9", TestHelpers.Get<GroupBox>(view, "competencyGroup").Text);
                Assert.Equal("", TestHelpers.Get<Label>(view, "levelInForce").Text);

                // An entry stays with its row when the level changes back and forth.
                competencies[0].Value = 12;
                level.Value = 11;
                Assert.Equal(13, competencies[5].Value);
                competencies[6].Value = 3;
                level.Value = 9;
                Assert.Equal(12, competencies[0].Value);
                level.Value = 11;
                TestHelpers.Get<NumericUpDown>(view, "birthYear").Value = 1980;
                TestHelpers.Get<NumericUpDown>(view, "nerves").Value = 60;
                Assert.Equal("19", TestHelpers.Get<Label>(view, "age").Text);
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));

                Assert.Equal("Gespeichert", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(11, m.Get(A3Addresses.ManagerLevel));
                Assert.Equal(60, m.GetUInt16(A3Addresses.ManagerNerves));
                Assert.Equal(12, m.GetUInt16(Cell(9, 0)));
                Assert.Equal(3, m.GetUInt16(Cell(11, 6)));
                Assert.Null(m.WrittenAt(Cell(11, 5)));
                Assert.Equal(1980, BitConverter.ToInt16(m.Read(Birthday + 2, 2), 0));
            }
        });

        [Fact]
        public void View_LevelBonus_OutOfRangeValues_AndErrors() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithPerson();
            m.Put(A3Addresses.ManagerLevelFlags, 0x0C);      // +3 +3
            m.PutUInt16(Cell(12, 2), 40);
            using (var view = new PersonView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal("im Spiel: Level 12", TestHelpers.Get<Label>(view, "levelInForce").Text);
                Assert.Equal(15, TestHelpers.Get<NumericUpDown[]>(view, "competencies")[2].Value);
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Null(m.WrittenAt(Cell(12, 2)));               // kept above the field's limit
                Assert.Null(m.WrittenAt(A3Addresses.ManagerNerves));

                TestHelpers.Get<NumericUpDown>(view, "birthMonth").Value = 2;
                TestHelpers.Get<NumericUpDown>(view, "birthDay").Value = 30;
                Assert.Equal("", TestHelpers.Get<Label>(view, "age").Text);
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Equal("Ungültiges Geburtsdatum", TestHelpers.Get<Label>(view, "status").Text);

                TestHelpers.Get<NumericUpDown>(view, "birthDay").Value = 1;
                TestHelpers.Set(view, "game", new A3Game(new ReadOnlyGameMemory(m)));
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Equal("Fehler beim Speichern", TestHelpers.Get<Label>(view, "status").Text);
            }

            using (var view = new PersonView(new ProcessMemory(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal("Kein Spiel verbunden", TestHelpers.Get<Label>(view, "status").Text);
                Assert.False(TestHelpers.Get<Button>(view, "save").Enabled);
            }
        });
    }
}
