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
            m.PutUInt32(club + A3Addresses.ClubSeasonStartMoney, 20000000);
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
                Assert.Equal(BitConverter.GetBytes(30000000), m.WrittenAt(A3Addresses.ClubArray + (uint)A3Addresses.ClubSize + A3Addresses.ClubSeasonStartMoney));

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
                Assert.Equal("Test", TestHelpers.Get<TextBox>(view, "firstName").Text);
                Assert.Equal("Trainer", TestHelpers.Get<TextBox>(view, "lastName").Text);
                NumericUpDown salary = TestHelpers.Get<NumericUpDown>(view, "managerSalary");
                Assert.Equal(375000, salary.Value);
                NumericUpDown money = TestHelpers.Get<NumericUpDown>(view, "privateMoney");
                Assert.Equal(49700, money.Value);

                money.Value = 1000000;
                salary.Value = 500000;
                Click(view, "save");
                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(BitConverter.GetBytes(1000000), m.WrittenAt(A3Addresses.ManagerPrivateMoney));
                Assert.Equal(BitConverter.GetBytes(500000), m.WrittenAt(A3Addresses.ManagerSalary));

                money.Value = 1;
                Click(view, "reload");
                Assert.Equal(1000000, money.Value);
            }
        });

        [Fact]
        public void ManagerView_Contract_GoesToTheManagerAndItsData() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            uint data = A3Addresses.ManagerData + A3Addresses.ManagerDataContract;
            m.Map(A3Addresses.ManagerSalary, A3Addresses.ContractSize);
            m.Map(data, A3Addresses.ContractSize);
            m.PutUInt32(A3Addresses.ManagerSalary, 375000);
            m.PutUInt32(A3Addresses.ManagerSalary + ContractOffset.PointBonus, 2500);
            m.PutUInt32(A3Addresses.ManagerSalary + ContractOffset.CupBonus, 6500);
            m.PutUInt32(A3Addresses.ManagerSalary + ContractOffset.TitleBonus, 40000);
            m.PutUInt32(A3Addresses.ManagerSalary + ContractOffset.GoalBonus, 22222);
            m.PutUInt16(A3Addresses.ManagerSalary + ContractOffset.Years, 2);
            using (var view = new ManagerView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal(2500, TestHelpers.Get<NumericUpDown>(view, "pointBonus").Value);
                Assert.Equal(6500, TestHelpers.Get<NumericUpDown>(view, "cupBonus").Value);
                Assert.Equal(40000, TestHelpers.Get<NumericUpDown>(view, "titleBonus").Value);
                Assert.Equal(22222, TestHelpers.Get<NumericUpDown>(view, "goalBonus").Value);
                ComboBox goal = TestHelpers.Get<ComboBox>(view, "seasonGoal");
                Assert.Equal("Meisterschaft", goal.Text);
                NumericUpDown years = TestHelpers.Get<NumericUpDown>(view, "contractYears");
                Assert.Equal(2, years.Value);
                CheckBox exit = TestHelpers.Get<CheckBox>(view, "exitClause");
                Assert.False(exit.Checked);

                TestHelpers.Get<NumericUpDown>(view, "pointBonus").Value = 3333;
                goal.SelectedIndex = 10;
                years.Value = 5;
                exit.Checked = true;
                Click(view, "save");

                Assert.Equal("Gespeichert", Status(view));
                foreach (uint contract in new[] { A3Addresses.ManagerSalary, data })
                {
                    Assert.Equal(3333, (int)m.ReadUInt32(contract + ContractOffset.PointBonus));
                    Assert.Null(m.WrittenAt(contract + 0x14)); // not the season goal
                    Assert.Equal(5, m.GetUInt16(contract + ContractOffset.Years));
                    Assert.Equal(1, m.Get(contract + ContractOffset.ExitClause));
                }
                Assert.Null(m.WrittenAt(data + ContractOffset.CupBonus)); // unchanged fields aren't written
                Assert.Equal("Klassenerhalt", goal.Text);
                Assert.Equal(10, m.Get(A3Addresses.ClubArray + (uint)A3Addresses.ClubSize + A3Addresses.ClubSeasonGoal)); // the club's
            }
        });

        [Fact]
        public void ManagerView_Contract_KeepsValuesTheFieldsCantShow() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            m.Map(A3Addresses.ManagerSalary, A3Addresses.ContractSize);
            m.Put(A3Addresses.ClubArray + (uint)A3Addresses.ClubSize + A3Addresses.ClubSeasonGoal, 40);
            m.PutUInt16(A3Addresses.ManagerSalary + ContractOffset.Years, 99);
            using (var view = new ManagerView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal(-1, TestHelpers.Get<ComboBox>(view, "seasonGoal").SelectedIndex);
                Assert.Equal(20, TestHelpers.Get<NumericUpDown>(view, "contractYears").Value);

                Click(view, "save");

                Assert.Null(m.WrittenAt(A3Addresses.ClubArray + (uint)A3Addresses.ClubSize + A3Addresses.ClubSeasonGoal));
                Assert.Null(m.WrittenAt(A3Addresses.ManagerSalary + ContractOffset.Years));
            }
        });

        [Fact]
        public void ClubView_SponsorIncome_GoesToRecordAndWorkingCopy() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            uint club = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;
            uint copy = 0x07000000;
            uint copyFinances = copy - (uint)A3Addresses.ClubCopyFinanceShift;
            m.Map(A3Addresses.ClubCache, A3Addresses.ClubCacheSlots * A3Addresses.ClubCacheEntrySize);
            m.Put(A3Addresses.ClubCache, 1, 14, 1);
            m.PutUInt32(A3Addresses.ClubCache + A3Addresses.ClubCacheObject, copy);
            m.Map(copy, 0x1390);
            m.PutUInt32(copyFinances + A3Addresses.ClubSeasonStartMoney, 10000000);
            m.Put(copyFinances + A3Addresses.ClubAdsContract, 1);
            m.PutUInt32(copyFinances + A3Addresses.ClubAds, 1344000);
            m.PutUInt32(copyFinances + A3Addresses.ClubOutfitter, 7200000); // no outfitter contract
            using (var view = new ClubView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                NumericUpDown ads = TestHelpers.Get<NumericUpDown>(view, "ads");
                NumericUpDown outfitter = TestHelpers.Get<NumericUpDown>(view, "outfitter");
                Assert.Equal(10000000, TestHelpers.Get<NumericUpDown>(view, "seasonStartMoney").Value);
                Assert.Equal(1344000, ads.Value);
                Assert.True(ads.Enabled);
                Assert.Equal(7200000, outfitter.Value);
                Assert.False(outfitter.Enabled);

                ads.Value = 2000000;
                Click(view, "save");

                Assert.Equal(BitConverter.GetBytes(2000000), m.WrittenAt(club + A3Addresses.ClubAds));
                Assert.Equal(BitConverter.GetBytes(2000000), m.WrittenAt(copyFinances + A3Addresses.ClubAds));
                Assert.Null(m.WrittenAt(copyFinances + A3Addresses.ClubOutfitter));
                Assert.Null(m.WrittenAt(copyFinances + A3Addresses.ClubSeasonStartMoney));
            }
        });

        [Fact]
        public void ClubView_ImageAndAssociation_GoToRecordAndWorkingCopy() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            uint club = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;
            uint copy = 0x07000000;
            m.Map(A3Addresses.ClubCache, A3Addresses.ClubCacheSlots * A3Addresses.ClubCacheEntrySize);
            m.Put(A3Addresses.ClubCache + A3Addresses.ClubCacheEntrySize, 1, 14, 10);  // the opponent first
            m.Put(A3Addresses.ClubCache + 2 * A3Addresses.ClubCacheEntrySize, 1, 14, 1);
            m.PutUInt32(A3Addresses.ClubCache + 2 * A3Addresses.ClubCacheEntrySize + A3Addresses.ClubCacheObject, copy);
            m.Map(copy, 0x1390);
            m.Put(club + A3Addresses.ClubImage, 20);                 // stale record
            m.Put(copy + A3Addresses.ClubCopyImage, 30);
            m.Put(copy + A3Addresses.ClubCopyTrouble, 0x86);         // trouble 6, a game state bit
            using (var view = new ClubView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                NumericUpDown image = TestHelpers.Get<NumericUpDown>(view, "image");
                NumericUpDown trouble = TestHelpers.Get<NumericUpDown>(view, "trouble");
                Assert.Equal(30, image.Value);
                Assert.Equal("neutral", TestHelpers.Get<Label>(view, "imageLevel").Text);
                Assert.Equal(6, trouble.Value);
                Assert.Equal("normal", TestHelpers.Get<Label>(view, "troubleLevel").Text);

                image.Value = 50;
                trouble.Value = 0;
                Assert.Equal("sehr beliebt", TestHelpers.Get<Label>(view, "imageLevel").Text);
                Assert.Equal("ausgezeichnet", TestHelpers.Get<Label>(view, "troubleLevel").Text);
                Click(view, "save");

                Assert.Equal(new byte[] { 50 }, m.WrittenAt(club + A3Addresses.ClubImage));
                Assert.Equal(new byte[] { 50 }, m.WrittenAt(copy + A3Addresses.ClubCopyImage));
                Assert.Equal(new byte[] { 0x80 }, m.WrittenAt(club + A3Addresses.ClubTrouble));
                Assert.Equal(new byte[] { 0x80 }, m.WrittenAt(copy + A3Addresses.ClubCopyTrouble));
            }
        });

        [Fact]
        public void ClubView_WithoutAWorkingCopy_UsesTheRecord() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            uint club = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;
            m.Put(club + A3Addresses.ClubImage, 12);
            m.Put(club + A3Addresses.ClubTrouble, 25);
            using (var view = new ClubView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal("vollkommen verhaßt", TestHelpers.Get<Label>(view, "imageLevel").Text);
                Assert.Equal("verzockt", TestHelpers.Get<Label>(view, "troubleLevel").Text);

                TestHelpers.Get<NumericUpDown>(view, "image").Value = 40;
                Click(view, "save");
                Assert.Equal(new byte[] { 40 }, m.WrittenAt(club + A3Addresses.ClubImage));
                Assert.Null(m.WrittenAt(club + A3Addresses.ClubTrouble));
            }
        });

        [Fact]
        public void ManagerView_BoardTrustAndPopularity() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            m.Put(A3Addresses.ManagerMediaPopularity, 47, 72, 120);  // media, public, fans above the field's limit
            m.Put(A3Addresses.ManagerBoardTrust, 79, 0, 1, 0, 0xFE, 0); // base 79, bonuses +1 and -2
            using (var view = new ManagerView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                NumericUpDown trust = TestHelpers.Get<NumericUpDown>(view, "boardTrust");
                NumericUpDown media = TestHelpers.Get<NumericUpDown>(view, "media");
                Assert.Equal(78, trust.Value);
                Assert.Equal(47, media.Value);
                Assert.Equal("Unbeliebt", TestHelpers.Get<Label>(view, "mediaLevel").Text);   // above 39, not above 49
                Assert.Equal("Angesehen", TestHelpers.Get<Label>(view, "publicLevel").Text);
                Assert.Equal(100, TestHelpers.Get<NumericUpDown>(view, "fans").Value);

                trust.Value = 100;
                media.Value = 96;
                Assert.Equal("Gottstatus", TestHelpers.Get<Label>(view, "mediaLevel").Text);
                Click(view, "save");

                Assert.Equal(BitConverter.GetBytes((short)101), m.WrittenAt(A3Addresses.ManagerBoardTrust));
                Assert.Equal(new byte[] { 96 }, m.WrittenAt(A3Addresses.ManagerMediaPopularity));
                Assert.Null(m.WrittenAt(A3Addresses.ManagerPublicPopularity));
                Assert.Null(m.WrittenAt(A3Addresses.ManagerFanPopularity));   // 120 stays

                TestHelpers.Get<NumericUpDown>(view, "fans").Value = 80;
                Click(view, "save");
                Assert.Equal(new byte[] { 80 }, m.WrittenAt(A3Addresses.ManagerFanPopularity));
            }
        });

        [Theory]
        [InlineData(9, 0x00, 9)]     // image level as it is
        [InlineData(5, 0x0C, 11)]    // +3 +3
        [InlineData(10, 0x80, 12)]   // +2, capped at 12
        [InlineData(13, 0x02, 12)]   // from 12 up only the -1 counts
        [InlineData(0, 0x02, 0)]     // -1, not below 0
        public void ManagerView_FansShowTheLevelTheGameShows(int level, int flags, int factorIndex) => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            m.Put(A3Addresses.ManagerFanPopularity, 60);
            m.Put(A3Addresses.ManagerImageLevel, (byte)level);
            m.Put(A3Addresses.ManagerImageFlags, (byte)flags);
            m.Map(A3Addresses.ManagerFanFactors, 16 * A3Addresses.ManagerFanFactorStride);
            for (int i = 0; i < 16; i++)
                m.PutUInt16(A3Addresses.ManagerFanFactors + (uint)(i * A3Addresses.ManagerFanFactorStride), (ushort)(i == factorIndex ? 10 : 0));
            using (var view = new ManagerView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                // The picked factor 10 gives 120 %: 60 → 72 ("Angesehen"); any other index 80 %: 60 → 48 ("Unbeliebt").
                Assert.Equal("Angesehen", TestHelpers.Get<Label>(view, "fansLevel").Text);
                TestHelpers.Get<NumericUpDown>(view, "fans").Value = 75;   // 90
                Assert.Equal("Supermann", TestHelpers.Get<Label>(view, "fansLevel").Text);
            }
        });

        [Fact]
        public void ManagerView_BoardTrustShowsTheBoardsVerdict() => StaThread.Run(() =>
        {
            FakeGameMemory m = CareerWithMoney();
            m.Put(A3Addresses.ManagerBoardTrust, 80, 0, 0, 0, 0, 0);
            using (var view = new ManagerView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Label verdict = TestHelpers.Get<Label>(view, "boardLevel");
                Assert.Equal("breites Vertrauen", verdict.Text);
                NumericUpDown trust = TestHelpers.Get<NumericUpDown>(view, "boardTrust");
                var expected = new[] { (100, "König von 1.FV München"), (90, "absolut unumstritten"), (70, "ziemliche Anzahl Kritiker"),
                    (60, "keine sichere Stellung mehr"), (55, "Rauswurf steht unmittelbar bevor"), (54, "ein Wunder, noch da zu sein") };
                foreach ((int percent, string text) in expected)
                {
                    trust.Value = percent;
                    Assert.Equal(text, verdict.Text);
                }
            }
        });

        [Fact]
        public void Views_EditNames_AnEmptiedNameStays() => StaThread.Run(() =>
        {
            FakeGameMemory m = A3GameTests.WithNamePool(CareerWithMoney());
            uint club = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;
            using (var view = new ClubView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                Assert.Equal(A3Addresses.ClubShortNameSize - 1, TestHelpers.Get<TextBox>(view, "clubShortName").MaxLength);
                TestHelpers.Get<TextBox>(view, "clubName").Text = " Bayern Sternstadt ";
                TestHelpers.Get<TextBox>(view, "clubCity").Text = "";
                Click(view, "save");
                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal("Bayern Sternstadt", TestHelpers.Get<TextBox>(view, "clubName").Text);
                Assert.Equal("Muenchen", TestHelpers.Get<TextBox>(view, "clubCity").Text);
                Assert.Null(m.WrittenAt(club + A3Addresses.ClubCity));
            }
            using (var view = new ManagerView(Attached(), new A3Game(m)))
            {
                view.RefreshValues();
                TestHelpers.Get<TextBox>(view, "firstName").Text = "Jupp";
                TestHelpers.Get<TextBox>(view, "lastName").Text = " ";
                Click(view, "save");
                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal("Jupp", TestHelpers.Get<TextBox>(view, "firstName").Text);
                Assert.Equal("Trainer", TestHelpers.Get<TextBox>(view, "lastName").Text);
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
