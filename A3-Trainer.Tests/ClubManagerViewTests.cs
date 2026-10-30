using A3_Trainer.Controller;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>The club and manager pages: showing, saving, reloading and their failure cases.</summary>
    public class ClubManagerViewTests
    {
        private static FakeGameMemory CareerWithMoney()
        {
            FakeGameMemory m = A3GameTests.Career();
            uint club = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;
            m.Put(club + A3Addresses.ClubCity, Encoding.ASCII.GetBytes("Muenchen\0"));
            m.PutUInt32(A3Addresses.SeasonStartMoney, 20000000);
            m.PutUInt32(A3Addresses.ManagerPrivateMoney, 49700);
            m.PutUInt32(A3Addresses.ManagerSalary, 375000);
            m.Put(A3Addresses.ManagerFirstName, Encoding.ASCII.GetBytes("Test\0\0\0\0\0\0"));
            m.Put(A3Addresses.ManagerLastName, Encoding.ASCII.GetBytes("Trainer\0\0\0\0\0\0\0\0\0"));
            return m;
        }

        // The test process stands in for the attached game; the data comes from the fake memory.
        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        private static string Status(Control view) => TestHelpers.Get<Label>(view, "status").Text;

        private static void Click(Control view, string button) => TestHelpers.Click(TestHelpers.Get<Button>(view, button));

        [Fact]
        public void ClubView_ShowsSavesAndReloadsTheClub() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            using (var view = new ClubView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal("1.FV München", TestHelpers.Get<TextBox>(view, "clubName").Text);
                Assert.Equal("Muenchen", TestHelpers.Get<TextBox>(view, "clubCity").Text);
                NumericUpDown money = TestHelpers.Get<NumericUpDown>(view, "seasonStartMoney");
                Assert.Equal(20000000, money.Value);

                money.Value = 30000000;
                Click(view, "save");
                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(BitConverter.GetBytes(30000000), m.WrittenAt(A3Addresses.SeasonStartMoney));

                money.Value = 1;
                Click(view, "reload");
                Assert.Equal(30000000, money.Value);
                Assert.Equal("", Status(view));
            }
        });

        [Fact]
        public void ManagerView_ShowsSavesAndReloadsTheManager() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            using (var view = new ManagerView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal("Test Trainer", TestHelpers.Get<TextBox>(view, "managerName").Text);
                Assert.Equal(375000.ToString("N0"), TestHelpers.Get<TextBox>(view, "managerSalary").Text);
                NumericUpDown money = TestHelpers.Get<NumericUpDown>(view, "privateMoney");
                Assert.Equal(49700, money.Value);

                money.Value = 1000000;
                Click(view, "save");
                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(BitConverter.GetBytes(1000000), m.WrittenAt(A3Addresses.ManagerPrivateMoney));

                money.Value = 1;
                Click(view, "reload");
                Assert.Equal(1000000, money.Value);
            }
        });

        [Fact]
        public void Views_WithoutAGame_AreEmptyAndCantSave() => StaThread.Run(() =>
        {
            using (var club = new ClubView(new ProcessMemory(), new A3Game(CareerWithMoney())))
            using (var manager = new ManagerView(new ProcessMemory(), new A3Game(CareerWithMoney())))
            {
                club.RefreshValues();
                manager.RefreshValues();

                Assert.Equal("Kein Spiel verbunden", Status(club));
                Assert.Equal("Kein Spiel verbunden", Status(manager));
                Assert.False(TestHelpers.Get<Button>(club, "save").Enabled);
                Assert.False(TestHelpers.Get<Button>(manager, "save").Enabled);

                // Nothing shown: saving does nothing.
                TestHelpers.Invoke(club, "Save");
                TestHelpers.Invoke(manager, "Save");
            }
        });

        [Fact]
        public void Views_WhenTheGameIsGoneBeforeSaving_ReportIt() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            ProcessMemory memory = Attached();
            using (var club = new ClubView(memory, new A3Game(m)))
            using (var manager = new ManagerView(memory, new A3Game(m)))
            {
                club.RefreshValues();
                manager.RefreshValues();
                memory.mProc.Process = null;

                TestHelpers.ExpectDialog("Error", () => TestHelpers.Invoke(club, "Save"));
                TestHelpers.ExpectDialog("Error", () => TestHelpers.Invoke(manager, "Save"));
                Assert.Empty(m.Writes);
            }
        });

        [Fact]
        public void Views_WhenWritingFails_SayThatSavingFailed() => StaThread.Run(() =>
        {
            var game = new A3Game(new ReadOnlyGameMemory(CareerWithMoney()));
            using (var club = new ClubView(Attached(), game))
            using (var manager = new ManagerView(Attached(), game))
            {
                club.RefreshValues();
                manager.RefreshValues();
                TestHelpers.Get<NumericUpDown>(club, "seasonStartMoney").Value = 1;
                TestHelpers.Get<NumericUpDown>(manager, "privateMoney").Value = 1;

                Click(club, "save");
                Click(manager, "save");

                Assert.Equal("Fehler beim Speichern", Status(club));
                Assert.Equal("Fehler beim Speichern", Status(manager));
            }
        });
    }
}
