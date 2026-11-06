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
    /// <summary>A club's overdraft and loans, and the interest rates every club pays or gets.</summary>
    public class FinancesTests
    {
        private const uint Copy = 0x07000000;
        private static readonly uint Munich = A3Addresses.ClubArray + (uint)A3Addresses.ClubSize;
        private static readonly uint Hamburg = A3Addresses.ClubArray + 10u * A3Addresses.ClubSize;
        private const uint CopyFinances = Copy - (uint)A3Addresses.ClubCopyFinanceShift;

        private static uint Loan(uint finances, int slot) =>
            finances + A3Addresses.ClubLoans + (uint)(slot * A3Addresses.ClubLoanSize);

        // München is loaded (its copy counts) and owes 3 + 2 million DM in slots 0 and 5; slot 2 holds an ended loan.
        // Hamburg isn't loaded and has an overdraft of 12.3 million DM. Rates as in the test career: 11.5, 0.75, 9.5, 9 %.
        internal static FakeGameMemory WithFinances()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.Map(A3Addresses.ClubCache, A3Addresses.ClubCacheSlots * A3Addresses.ClubCacheEntrySize);
            m.Put(A3Addresses.ClubCache, 1, 14, 1);
            m.PutUInt32(A3Addresses.ClubCache + A3Addresses.ClubCacheObject, Copy);
            m.Map(Copy, 0x1390);
            foreach (uint finances in new[] { CopyFinances, Munich })
            {
                m.Put(finances + A3Addresses.ClubSeasonStartMoney, BitConverter.GetBytes(10000000L));
                m.PutUInt16(finances + A3Addresses.ClubOverdraft, 344);
                m.Put(Loan(finances, 0), BitConverter.GetBytes(3000000L));
                m.Put(Loan(finances, 0) + A3Addresses.ClubLoanFlags, 3);
                m.Put(Loan(finances, 2), BitConverter.GetBytes(7000000L));
                m.Put(Loan(finances, 5), BitConverter.GetBytes(2000000L));
                m.Put(Loan(finances, 5) + A3Addresses.ClubLoanFlags, 1);
            }
            m.PutUInt16(Hamburg + A3Addresses.ClubOverdraft, 123);
            m.Map(A3Addresses.InterestRate, 8);
            m.Put(A3Addresses.InterestRate, BitConverter.GetBytes((short)1150));
            m.Put(A3Addresses.SavingsRate, BitConverter.GetBytes((short)75));
            m.Put(A3Addresses.OverdraftRate, BitConverter.GetBytes((short)950));
            m.Put(A3Addresses.LoanRate, BitConverter.GetBytes((short)900));
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        private static T Field<T>(object owner, string name) => TestHelpers.Get<T>(owner, name);

        [Fact]
        public void ReadFinances_CountsRunningLoans_FromTheLoadedCopy()
        {
            FakeGameMemory m = WithFinances();
            m.PutUInt16(Munich + A3Addresses.ClubOverdraft, 1); // stale record
            ClubFinances munich = new A3Game(m).ReadFinances(new ClubKey(14, 1));

            Assert.Equal(34400000, munich.Overdraft);
            Assert.Equal(2, munich.LoanCount);
            Assert.Equal(5000000, munich.LoanTotal);

            ClubFinances hamburg = new A3Game(m).ReadFinances(new ClubKey(14, 10));
            Assert.Equal(12300000, hamburg.Overdraft);
            Assert.Equal(0, hamburg.LoanCount);
            Assert.Null(new A3Game(m).ReadFinances(new ClubKey(14, 50)));
        }

        [Fact]
        public void SaveFinances_WritesTheOverdraftToCopyAndRecord_OnlyIfChanged()
        {
            FakeGameMemory m = WithFinances();
            var game = new A3Game(m);
            ClubFinances original = game.ReadFinances(new ClubKey(14, 1));
            game.SaveFinances(original, original.Clone());
            Assert.Empty(m.Writes);

            ClubFinances edited = original.Clone();
            edited.Overdraft = 50000000;
            game.SaveFinances(original, edited);
            Assert.Equal(new byte[] { 0xF4, 0x01 }, m.WrittenAt(CopyFinances + A3Addresses.ClubOverdraft));
            Assert.Equal(new byte[] { 0xF4, 0x01 }, m.WrittenAt(Munich + A3Addresses.ClubOverdraft));
        }

        [Fact]
        public void ForgiveLoans_EndsRunningLoans_AndKeepsTheBalance()
        {
            FakeGameMemory m = WithFinances();
            var game = new A3Game(m);

            Assert.Equal(5000000, game.ForgiveLoans(new ClubKey(14, 1)));

            foreach (uint finances in new[] { CopyFinances, Munich })
            {
                Assert.Equal(new byte[8], m.WrittenAt(Loan(finances, 0)));
                Assert.Equal(new byte[1], m.WrittenAt(Loan(finances, 5) + A3Addresses.ClubLoanFlags));
                Assert.Null(m.WrittenAt(Loan(finances, 2)));
                Assert.Equal(BitConverter.GetBytes(15000000L), m.WrittenAt(finances + A3Addresses.ClubSeasonStartMoney));
            }
            Assert.Equal(0, game.ReadFinances(new ClubKey(14, 1)).LoanCount);

            // Nothing to forgive, or no club: nothing is written.
            int writes = m.Writes.Count;
            Assert.Equal(0, game.ForgiveLoans(new ClubKey(14, 10)));
            Assert.Equal(0, game.ForgiveLoans(new ClubKey(14, 50)));
            Assert.Equal(writes, m.Writes.Count);
        }

        [Fact]
        public void Rates_AreReadAndOnlyChangedOnesWritten()
        {
            FakeGameMemory m = WithFinances();
            var game = new A3Game(m);
            InterestRates rates = game.ReadRates();
            Assert.Equal(new[] { 1150, 950, 75, 900 }, new[] { rates.Interest, rates.Overdraft, rates.Savings, rates.Loan });

            InterestRates edited = rates.Clone();
            edited.Savings = 500;
            game.SaveRates(rates, edited);
            Assert.Equal(new byte[] { 0xF4, 0x01 }, m.WrittenAt(A3Addresses.SavingsRate));
            Assert.Single(m.Writes);

            Assert.Equal(0, new A3Game(new FakeGameMemory()).ReadRates().Interest);
        }

        [Fact]
        public void FinancesView_ShowsSavesAndForgives() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithFinances();
            using (var view = new FinancesView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal(34.4m, Field<NumericUpDown>(view, "overdraft").Value);
                Assert.Equal("2 über 5.000.000 DM", Field<Label>(view, "loans").Text);
                Assert.Equal(11.5m, Field<NumericUpDown>(view, "interest").Value);
                Assert.Equal(0.75m, Field<NumericUpDown>(view, "savings").Value);

                Field<NumericUpDown>(view, "overdraft").Value = 40.04m;  // whole 100,000s
                Field<NumericUpDown>(view, "interest").Value = 10m;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal("Gespeichert", Field<Label>(view, "status").Text);
                Assert.Equal(new byte[] { 0x90, 0x01 }, m.WrittenAt(Munich + A3Addresses.ClubOverdraft));
                Assert.Equal(BitConverter.GetBytes((short)1000), m.WrittenAt(A3Addresses.InterestRate));
                Assert.Null(m.WrittenAt(A3Addresses.OverdraftRate));

                TestHelpers.Click(Field<Button>(view, "forgive"));
                Assert.Equal("5.000.000 DM Kredite erlassen", Field<Label>(view, "status").Text);
                Assert.Equal("keine", Field<Label>(view, "loans").Text);
                Assert.False(Field<Button>(view, "forgive").Enabled);
            }
        });

        [Fact]
        public void FinancesView_WithoutClubOrGame_ShowsNothing_AndReportsFailures() => StaThread.Run(() =>
        {
            using (var view = new FinancesView(new ProcessMemory(), new A3Game(WithFinances())) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal("Kein Verein gefunden", Field<Label>(view, "status").Text);
                Assert.False(Field<Button>(view, "save").Enabled);
                TestHelpers.Invoke(view, "Save");
                TestHelpers.Invoke(view, "Forgive");
            }
            using (var view = new FinancesView(Attached(), new A3Game(new ReadOnlyGameMemory(WithFinances()))) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Field<NumericUpDown>(view, "savings").Value = 1;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal("Fehler beim Speichern", Field<Label>(view, "status").Text);
                TestHelpers.Click(Field<Button>(view, "forgive"));
                Assert.Equal("Fehler beim Erlassen", Field<Label>(view, "status").Text);
            }
        });
    }
}
