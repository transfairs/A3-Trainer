using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>A club's investments and the investment partners' indices.</summary>
    public class InvestmentsTests
    {
        private const uint Copy = 0x07000000;
        private static readonly uint Munich = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;
        private const uint CopyFinances = Copy - (uint)A3Addresses.ClubCopyFinanceShift;

        private static uint Slot(uint finances, int slot) =>
            finances + A3Addresses.ClubInvestments + (uint)(slot * A3Addresses.ClubInvestmentSize);

        // München (loaded) has 100,000 DM with Kostol & Any in slot 1, bought at index 1000, now 1500; slot 3 was sold.
        // It is 15 August.
        internal static FakeGameMemory WithInvestments()
        {
            FakeGameMemory m = FinancesTests.WithFinances();
            foreach (uint finances in new[] { CopyFinances, Munich })
            {
                m.Put(Slot(finances, 1), Entry(100000, 100000, 1000, 1, 1));
                m.Put(Slot(finances, 3), Entry(50000, 50000, 1000, 2, 0));
            }
            m.Map(A3Addresses.InvestmentIndices, 20);
            foreach (var (partner, index) in new[] { (0, 1000), (1, 1500), (2, 900), (3, 1000), (4, 1200) })
                m.PutUInt32(A3Addresses.InvestmentIndices + (uint)(partner * 4), (uint)index);
            m.PutUInt16(A3Addresses.CurrentDay, 15);
            m.PutUInt16(A3Addresses.CurrentMonth, 8);
            return m;
        }

        private static byte[] Entry(long paid, long baseValue, short index, byte partner, byte flags)
        {
            var entry = new byte[A3Addresses.ClubInvestmentSize];
            BitConverter.GetBytes(paid).CopyTo(entry, 0);
            BitConverter.GetBytes(baseValue).CopyTo(entry, A3Addresses.InvestmentBase);
            BitConverter.GetBytes(index).CopyTo(entry, A3Addresses.InvestmentIndex);
            entry[A3Addresses.InvestmentPartner] = partner;
            entry[A3Addresses.InvestmentFlags] = flags;
            return entry;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        [Fact]
        public void Investment_ValueFollowsThePartnersIndex()
        {
            var investment = new Investment { Base = 100000, IndexAtPurchase = 1000 };
            Assert.Equal(150000, investment.ValueAt(1500));
            Assert.Equal(200000, investment.BaseFor(300000, 1500));
            Assert.Equal(300000, investment.BaseFor(300000, 0));
            var unpriced = new Investment { Base = 5000 };
            Assert.Equal(5000, unpriced.ValueAt(1500));
            Assert.Equal(7000, unpriced.BaseFor(7000, 1500));
        }

        [Fact]
        public void ReadInvestments_ListsTheRunningOnes()
        {
            var game = new A3Game(WithInvestments());
            Investment kostol = Assert.Single(game.ReadInvestments(new ClubKey(14, 1)));
            Assert.Equal(1, kostol.Slot);
            Assert.Equal(1, kostol.Partner);
            Assert.Equal(100000, kostol.Paid);
            Assert.Equal(1000, kostol.IndexAtPurchase);
            Assert.Equal(new[] { 1000, 1500, 900, 1000, 1200 }, game.ReadInvestmentIndices());
            Assert.Empty(game.ReadInvestments(new ClubKey(14, 10)));
            Assert.Empty(game.ReadInvestments(new ClubKey(14, 50)));
        }

        [Fact]
        public void SetInvestmentValue_ChangesTheBase_InCopyAndRecord()
        {
            FakeGameMemory m = WithInvestments();
            var game = new A3Game(m);
            Investment kostol = game.ReadInvestments(new ClubKey(14, 1))[0];

            game.SetInvestmentValue(new ClubKey(14, 1), kostol, 300000);

            foreach (uint finances in new[] { CopyFinances, Munich })
                Assert.Equal(BitConverter.GetBytes(200000L), m.WrittenAt(Slot(finances, 1) + A3Addresses.InvestmentBase));
        }

        [Fact]
        public void AddInvestment_FillsTheFirstFreeSlot_LikeTheGame()
        {
            FakeGameMemory m = WithInvestments();
            var game = new A3Game(m);

            Assert.Equal(0, game.AddInvestment(new ClubKey(14, 1), 4, 250000));

            byte[] expected = Entry(250000, 250000, 1200, 4, 1);
            BitConverter.GetBytes((short)45).CopyTo(expected, A3Addresses.InvestmentDay);    // 15 August: day 45 of the quarter
            Assert.Equal(expected, m.WrittenAt(Slot(CopyFinances, 0)));
            Assert.Equal(expected, m.WrittenAt(Slot(Munich, 0)));

            // Then slot 2 (3 was sold and is free too), until all six are taken.
            Assert.Equal(2, game.AddInvestment(new ClubKey(14, 1), 0, 1000));
            Assert.Equal(3, game.AddInvestment(new ClubKey(14, 1), 0, 1000));
            Assert.Equal(4, game.AddInvestment(new ClubKey(14, 1), 0, 1000));
            Assert.Equal(5, game.AddInvestment(new ClubKey(14, 1), 0, 1000));
            Assert.Equal(-1, game.AddInvestment(new ClubKey(14, 1), 0, 1000));
            Assert.Equal(-1, game.AddInvestment(new ClubKey(14, 50), 0, 1000));
        }

        [Fact]
        public void SaveInvestmentIndices_WritesOnlyChangedOnes()
        {
            FakeGameMemory m = WithInvestments();
            var game = new A3Game(m);
            int[] indices = game.ReadInvestmentIndices();
            int[] edited = (int[])indices.Clone();
            edited[2] = 2000;
            int writes = m.Writes.Count;

            game.SaveInvestmentIndices(indices, edited);

            Assert.Equal(writes + 1, m.Writes.Count);
            Assert.Equal(BitConverter.GetBytes(2000), m.WrittenAt(A3Addresses.InvestmentIndices + 8));
        }

        [Fact]
        public void View_ShowsSetsAndAdds() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithInvestments();
            using (var view = new InvestmentsView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                _ = view.Handle;
                view.RefreshValues();
                ListView list = TestHelpers.Get<ListView>(view, "list");
                ListViewItem row = Assert.Single(list.Items.Cast<ListViewItem>());
                Assert.Equal(new[] { "Kostol & Any", "100.000", "150.000", "50.000" }, row.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text));
                Assert.False(TestHelpers.Get<Button>(view, "setValue").Enabled);
                Assert.Equal(1500, TestHelpers.Get<NumericUpDown[]>(view, "indices")[1].Value);

                row.Selected = true;
                Assert.True(TestHelpers.Get<Button>(view, "setValue").Enabled);
                Assert.Equal(150000, TestHelpers.Get<NumericUpDown>(view, "amount").Value);
                Assert.Equal("Kostol & Any", TestHelpers.Get<ComboBox>(view, "partner").Text);
                TestHelpers.Get<NumericUpDown>(view, "amount").Value = 450000;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "setValue"));
                Assert.Equal("Wert gesetzt", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(BitConverter.GetBytes(300000L), m.WrittenAt(Slot(Munich, 1) + A3Addresses.InvestmentBase));
                Assert.Equal("450.000", list.Items[0].SubItems[2].Text);
                Assert.True(list.Items[0].Selected);    // the selection stays

                list.Items[0].Selected = false;
                TestHelpers.Get<ComboBox>(view, "partner").SelectedIndex = 2;
                TestHelpers.Get<NumericUpDown>(view, "amount").Value = 80000;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "add"));
                Assert.Equal("Angelegt", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(2, list.Items.Count);

                TestHelpers.Get<NumericUpDown[]>(view, "indices")[4].Value = 1300;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "saveIndices"));
                Assert.Equal("Indizes gesetzt", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(BitConverter.GetBytes(1300), m.WrittenAt(A3Addresses.InvestmentIndices + 16));
            }
        });

        [Fact]
        public void View_ReportsFullSlotsMissingClubsAndFailures() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithInvestments();
            var game = new A3Game(m);
            for (int i = 0; i < 5; i++)
                game.AddInvestment(new ClubKey(14, 1), 0, 1000);
            using (var view = new InvestmentsView(Attached(), game) { Club = new ClubKey(14, 1) })
            {
                _ = view.Handle;
                view.RefreshValues();
                TestHelpers.Get<NumericUpDown>(view, "amount").Value = 1000;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "add"));
                Assert.Equal("Alle sechs Anlageplätze sind belegt", TestHelpers.Get<Label>(view, "status").Text);

                TestHelpers.Set(view, "game", new A3Game(new ReadOnlyGameMemory(m)));
                TestHelpers.Click(TestHelpers.Get<Button>(view, "saveIndices"));
                Assert.Equal("Indizes gesetzt", TestHelpers.Get<Label>(view, "status").Text);   // nothing changed, nothing written
                TestHelpers.Get<NumericUpDown[]>(view, "indices")[0].Value = 999;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "saveIndices"));
                Assert.Equal("Fehler beim Speichern", TestHelpers.Get<Label>(view, "status").Text);

                view.Club = new ClubKey(14, 50);
                view.RefreshValues();
                Assert.Equal("Kein Verein gefunden", TestHelpers.Get<Label>(view, "status").Text);
                Assert.False(TestHelpers.Get<Button>(view, "add").Enabled);
                Assert.Empty(TestHelpers.Get<ListView>(view, "list").Items);
            }
            using (var view = new InvestmentsView(new ProcessMemory(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal("Kein Verein gefunden", TestHelpers.Get<Label>(view, "status").Text);
                TestHelpers.Invoke(view, "SetValue");
                TestHelpers.Invoke(view, "Add");
                TestHelpers.Invoke(view, "SaveIndices");
            }
        });
    }
}
