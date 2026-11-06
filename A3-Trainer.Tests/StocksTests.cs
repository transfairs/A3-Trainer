using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>The stock market and the human managers' depots.</summary>
    public class StocksTests
    {
        private static uint Entry(int stock) => A3Addresses.StockMarket + A3Addresses.StockEntries + (uint)(stock * A3Addresses.StockSize);
        private static uint Depot(int manager) => A3Addresses.Depot + (uint)((manager - 1) * A3Addresses.DepotStride);
        private static uint Slot(int manager, int slot) => Depot(manager) + A3Addresses.DepotSlotsAt + (uint)(slot * A3Addresses.DepotSlotSize);

        // Three stocks: FC Leverkusen (14/4, 3,743,100 shares, 290.54 this week), the companies Aligas and Ascaron.
        // Manager 1 holds 6 Leverkusen shares in the last slot, as the game puts a first purchase.
        internal static FakeGameMemory WithMarket()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.Map(A3Addresses.StockMarket, A3Addresses.StockEntries + 3 * A3Addresses.StockSize);
            m.PutUInt32(A3Addresses.StockMarket + A3Addresses.StockCount, 3);
            void Stock(int index, string name, short country, short club, int shares, int week, float price)
            {
                m.Put(Entry(index), Encoding.GetEncoding(1252).GetBytes(name + "\0"));
                m.Put(Entry(index) + A3Addresses.StockCountry, BitConverter.GetBytes(country));
                m.Put(Entry(index) + A3Addresses.StockClub, BitConverter.GetBytes(club));
                m.PutUInt32(Entry(index) + A3Addresses.StockShares, (uint)shares);
                m.PutUInt16(Entry(index) + A3Addresses.StockWeek, week);
                m.Put(Entry(index) + A3Addresses.StockHistory + (uint)(week * 4), BitConverter.GetBytes(price));
            }
            Stock(0, "FC Leverkusen", 14, 4, 3743100, 49, 290.54f);
            Stock(1, "Aligas", -1, 2, 19000000, 49, 406.53f);
            Stock(2, "Ascaron", -1, 5, 13000000, 99, 1f);  // a week outside the history reads as no price
            m.Map(A3Addresses.Depot, 4 * A3Addresses.DepotStride);
            m.Put(Depot(1) + A3Addresses.DepotUsed + 49, 1);
            m.Put(Slot(1, 49), BitConverter.GetBytes(0));
            m.Put(Slot(1, 49) + 4, BitConverter.GetBytes(290.54f));
            m.PutUInt32(Slot(1, 49) + 8, 6);
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
        public void ReadStocks_TakesNamesClubsSharesAndThisWeeksPrice()
        {
            List<StockQuote> stocks = new A3Game(WithMarket()).ReadStocks();

            Assert.Equal(new[] { "FC Leverkusen", "Aligas", "Ascaron" }, stocks.Select(s => s.Name));
            Assert.Equal(new ClubKey(14, 4), stocks[0].Club);
            Assert.Null(stocks[1].Club);
            Assert.Equal(3743100, stocks[0].Shares);
            Assert.Equal(290.54f, stocks[0].Price);
            Assert.Equal(0f, stocks[2].Price);
            Assert.Equal("Aligas", stocks[1].ToString());

            FakeGameMemory none = A3GameTests.Career();
            Assert.Empty(new A3Game(none).ReadStocks());
            none.Map(A3Addresses.StockMarket, A3Addresses.StockEntries);
            none.PutUInt32(A3Addresses.StockMarket + A3Addresses.StockCount, 2);   // entries unreadable
            Assert.Empty(new A3Game(none).ReadStocks());
        }

        [Fact]
        public void SetStockPrice_WritesThisWeeksPrice_OnlyForAWeekInTheHistory()
        {
            FakeGameMemory m = WithMarket();
            var game = new A3Game(m);
            game.SetStockPrice(0, 300f);
            Assert.Equal(BitConverter.GetBytes(300f), m.WrittenAt(Entry(0) + A3Addresses.StockHistory + 49 * 4));

            int writes = m.Writes.Count;
            game.SetStockPrice(2, 5f);
            Assert.Equal(writes, m.Writes.Count);
        }

        [Fact]
        public void Depot_ReadsSavesAddsAndRemovesPositions()
        {
            FakeGameMemory m = WithMarket();
            var game = new A3Game(m);
            DepotPosition held = Assert.Single(game.ReadDepot(1));
            Assert.Equal(new object[] { 49, 0, 6, 290.54f }, new object[] { held.Slot, held.Stock, held.Shares, held.PurchasePrice });
            Assert.Empty(game.ReadDepot(2));

            held.Shares = 2000000;
            Assert.Equal(49, game.SavePosition(1, held));
            Assert.Equal(2000000, (int)m.ReadUInt32(Slot(1, 49) + 8));

            // A new position takes the last free slot.
            Assert.Equal(48, game.SavePosition(1, new DepotPosition { Slot = -1, Stock = 1, Shares = 10, PurchasePrice = 400f }));
            Assert.Equal(new byte[] { 1 }, m.WrittenAt(Depot(1) + A3Addresses.DepotUsed + 48));
            Assert.Equal(2, game.ReadDepot(1).Count);

            game.RemovePosition(1, 49);
            Assert.Equal(new byte[] { 0 }, m.WrittenAt(Depot(1) + A3Addresses.DepotUsed + 49));
            Assert.Equal(new byte[A3Addresses.DepotSlotSize], m.WrittenAt(Slot(1, 49)));
            Assert.Equal(48, Assert.Single(game.ReadDepot(1)).Slot);

            // A full depot, or none at all, takes no new position.
            for (int slot = 0; slot < A3Addresses.DepotSlots; slot++)
                m.Put(Depot(3) + A3Addresses.DepotUsed + (uint)slot, 1);
            Assert.Equal(-1, game.SavePosition(3, new DepotPosition { Slot = -1 }));
            Assert.Equal(-1, new A3Game(A3GameTests.Career()).SavePosition(1, new DepotPosition { Slot = -1 }));
            Assert.Empty(new A3Game(A3GameTests.Career()).ReadDepot(1));
        }

        [Fact]
        public void StocksView_ShowsTheDepot_AndEditsAddsRemovesAndSetsThePrice() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithMarket();
            using (var view = new StocksView(Attached(), new A3Game(m)) { Manager = 1 })
            {
                ListView depot = Field<ListView>(view, "depot");
                _ = depot.Handle;
                view.RefreshValues();
                ComboBox stock = Field<ComboBox>(view, "stock");
                Assert.Equal(new[] { "FC Leverkusen", "6", "290,54", "290,54", "1.743" },
                    depot.Items[0].SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text));
                Assert.Equal(new[] { "FC Leverkusen", "Aligas", "Ascaron" }, stock.Items.Cast<object>().Select(o => o.ToString()));
                Assert.Equal("1 von 50 Depotplätzen belegt", Field<Label>(view, "status").Text);
                Assert.False(Field<Button>(view, "apply").Enabled);

                depot.Items[0].Selected = true;
                Assert.Same(stock.Items[0], stock.SelectedItem);
                Field<NumericUpDown>(view, "shares").Value = 2000000;
                Assert.Equal("53,43 % der Aktien", Field<Label>(view, "stake").Text);
                TestHelpers.Click(Field<Button>(view, "apply"));
                Assert.Equal("Gespeichert", Field<Label>(view, "status").Text);
                Assert.Equal(2000000, (int)m.ReadUInt32(Slot(1, 49) + 8));
                Assert.True(depot.Items[0].Selected);

                stock.SelectedIndex = 1;   // Aligas as a new position
                Assert.Equal(406.53m, Field<NumericUpDown>(view, "price").Value);
                Field<NumericUpDown>(view, "shares").Value = 5;
                TestHelpers.Click(Field<Button>(view, "add"));
                Assert.Equal(2, depot.Items.Count);
                Assert.Equal(1, (int)m.ReadUInt32(Slot(1, 48)));

                Field<NumericUpDown>(view, "price").Value = 410m;
                TestHelpers.Click(Field<Button>(view, "setPrice"));
                Assert.Equal("Kurs gesetzt", Field<Label>(view, "status").Text);
                Assert.Equal(BitConverter.GetBytes(410f), m.WrittenAt(Entry(1) + A3Addresses.StockHistory + 49 * 4));
                Assert.Equal("Aligas", stock.Text);

                depot.Items[0].Selected = true;
                TestHelpers.Click(Field<Button>(view, "remove"));
                Assert.Equal("Entfernt", Field<Label>(view, "status").Text);
                Assert.Single(depot.Items);

                stock.SelectedIndex = 2;  // a stock without shares issued shows no stake
                Field<NumericUpDown>(view, "shares").Value = 1;
            }
        });

        [Fact]
        public void StocksView_FullDepot_NoGame_AndFailures() => StaThread.Run(() =>
        {
            FakeGameMemory full = WithMarket();
            for (int slot = 0; slot < A3Addresses.DepotSlots; slot++)
                full.Put(Depot(1) + A3Addresses.DepotUsed + (uint)slot, 1);
            using (var view = new StocksView(Attached(), new A3Game(full)) { Manager = 1 })
            {
                view.RefreshValues();
                Field<ComboBox>(view, "stock").SelectedIndex = 0;
                TestHelpers.Click(Field<Button>(view, "add"));
                Assert.Equal("Das Depot ist voll", Field<Label>(view, "status").Text);
                TestHelpers.Invoke(view, "Save", (int?)null);   // nothing selected: nothing happens
            }
            using (var view = new StocksView(new ProcessMemory(), new A3Game(WithMarket())))
            {
                view.RefreshValues();
                Assert.Equal("Kein Spiel verbunden", Field<Label>(view, "status").Text);
                TestHelpers.Invoke(view, "Remove");
                TestHelpers.Invoke(view, "SetPrice");
            }
            using (var view = new StocksView(Attached(), new A3Game(A3GameTests.Career())))
            {
                view.RefreshValues();
                Assert.Equal("Kein Aktienmarkt gefunden", Field<Label>(view, "status").Text);
            }
            using (var view = new StocksView(Attached(), new A3Game(new ReadOnlyGameMemory(WithMarket()))) { Manager = 1 })
            {
                ListView depot = Field<ListView>(view, "depot");
                _ = depot.Handle;
                view.RefreshValues();
                depot.Items[0].Selected = true;
                TestHelpers.Click(Field<Button>(view, "apply"));
                Assert.Equal("Fehler beim Speichern", Field<Label>(view, "status").Text);
                TestHelpers.Click(Field<Button>(view, "remove"));
                Assert.Equal("Fehler beim Entfernen", Field<Label>(view, "status").Text);
                TestHelpers.Click(Field<Button>(view, "setPrice"));
                Assert.Equal("Fehler beim Setzen des Kurses", Field<Label>(view, "status").Text);
            }
        });
    }
}
