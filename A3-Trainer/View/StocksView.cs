using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A human manager's depot: positions with shares and purchase price, and the current price of a stock.</summary>
    internal sealed class StocksView : TrainerView
    {
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        private readonly A3Game game;

        private readonly ListView depot = new ListView();
        private readonly ComboBox stock = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown shares = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, ThousandsSeparator = true };
        private readonly NumericUpDown purchasePrice = Price();
        private readonly NumericUpDown price = Price();
        private readonly Label stake = new Label();
        private readonly Button apply = new Button { Text = "Übernehmen" };
        private readonly Button add = new Button { Text = "Neu ins Depot" };
        private readonly Button remove = new Button { Text = "Entfernen" };
        private readonly Button setPrice = new Button { Text = "Kurs setzen" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private List<StockQuote> stocks = new List<StockQuote>();
        private List<DepotPosition> positions = new List<DepotPosition>();

        /// <summary>Creates the stocks page, reading through <paramref name="game"/>.</summary>
        internal StocksView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(614, 389);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The human manager (1-4) whose depot the page shows.</summary>
        internal int Manager { get; set; } = 1;

        private void BuildLayout()
        {
            this.depot.SetBounds(8, 8, 598, 170);
            this.depot.View = System.Windows.Forms.View.Details;
            this.depot.FullRowSelect = true;
            this.depot.HideSelection = false;
            this.depot.MultiSelect = false;
            this.depot.Columns.Add("Aktie", 200);
            this.depot.Columns.Add("Anzahl", 90, HorizontalAlignment.Right);
            this.depot.Columns.Add("Kaufkurs", 90, HorizontalAlignment.Right);
            this.depot.Columns.Add("Kurs", 90, HorizontalAlignment.Right);
            this.depot.Columns.Add("Wert (DM)", 104, HorizontalAlignment.Right);
            this.depot.SelectedIndexChanged += (s, e) => this.ShowSelected();
            this.Controls.Add(this.depot);

            var position = new GroupBox { Text = "Position", Bounds = new Rectangle(8, 184, 598, 150) };
            int y = 22;
            void Row(string caption, Control input, int width)
            {
                position.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 110, 18) });
                input.SetBounds(126, y, width, 21);
                position.Controls.Add(input);
                y += 30;
            }
            Row("Aktie", this.stock, 220);
            Row("Anzahl", this.shares, 120);
            Row("Kaufkurs (DM)", this.purchasePrice, 120);
            Row("Aktueller Kurs (DM)", this.price, 120);
            this.stake.SetBounds(254, 55, 200, 18);
            this.setPrice.SetBounds(254, 110, 110, 25);
            this.apply.SetBounds(474, 20, 112, 25);
            this.add.SetBounds(474, 50, 112, 25);
            this.remove.SetBounds(474, 80, 112, 25);
            position.Controls.AddRange(new Control[] { this.stake, this.setPrice, this.apply, this.add, this.remove });
            this.Controls.Add(position);

            this.stock.SelectedIndexChanged += (s, e) => this.ShowStock();
            this.shares.ValueChanged += (s, e) => this.ShowStake();
            this.apply.Click += (s, e) => this.Save(this.Selected()?.Slot);
            this.add.Click += (s, e) => this.Save(-1);
            this.remove.Click += (s, e) => this.Remove();
            this.setPrice.Click += (s, e) => this.SetPrice();
            this.reload.SetBounds(8, 342, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(124, 346, 482, 20);
            this.Controls.AddRange(new Control[] { this.reload, this.status });
        }

        /// <summary>Reads the market and the depot from the game (or clears the view if none is attached).</summary>
        internal void RefreshValues()
        {
            int? selectedSlot = this.Selected()?.Slot;
            bool running = this.IsGameRunning(silent: true);
            this.stocks = running ? this.game.ReadStocks() : new List<StockQuote>();
            this.positions = running ? this.game.ReadDepot(this.Manager) : new List<DepotPosition>();
            // Clubs first, then the companies, each by name.
            this.stock.Items.Clear();
            this.stock.Items.AddRange(this.stocks.OrderBy(s => s.Club.HasValue ? 0 : 1).ThenBy(s => s.Name, StringComparer.CurrentCulture)
                .Cast<object>().ToArray());

            this.depot.BeginUpdate();
            try
            {
                this.depot.Items.Clear();
                foreach (DepotPosition position in this.positions.OrderBy(p => p.Slot))
                {
                    StockQuote quote = this.Quote(position.Stock);
                    this.depot.Items.Add(new ListViewItem(new[]
                    {
                        quote?.Name ?? $"Aktie {position.Stock}",
                        position.Shares.ToString("N0", German),
                        position.PurchasePrice.ToString("N2", German),
                        quote == null ? "" : quote.Price.ToString("N2", German),
                        quote == null ? "" : ((decimal)quote.Price * position.Shares).ToString("N0", German),
                    }) { Tag = position, Selected = position.Slot == selectedSlot });
                }
            }
            finally
            {
                this.depot.EndUpdate();
            }

            this.add.Enabled = this.setPrice.Enabled = this.stocks.Count > 0;
            this.ShowSelected();
            this.status.Text = !running ? "Kein Spiel verbunden"
                : this.stocks.Count == 0 ? "Kein Aktienmarkt gefunden"
                : $"{this.positions.Count} von {A3Addresses.DepotSlots} Depotplätzen belegt";
        }

        private StockQuote Quote(int index) => this.stocks.FirstOrDefault(s => s.Index == index);

        private DepotPosition Selected() =>
            this.depot.SelectedItems.Count > 0 ? (DepotPosition)this.depot.SelectedItems[0].Tag : null;

        // The selected position fills the inputs; without one they stay for a new position.
        private void ShowSelected()
        {
            DepotPosition position = this.Selected();
            this.apply.Enabled = this.remove.Enabled = position != null;
            if (position == null)
                return;
            this.stock.SelectedItem = this.Quote(position.Stock);
            SetValue(this.shares, position.Shares);
            SetValue(this.purchasePrice, (decimal)position.PurchasePrice);
        }

        private void ShowStock()
        {
            if (this.stock.SelectedItem is StockQuote quote)
                SetValue(this.price, (decimal)quote.Price);
            this.ShowStake();
        }

        private void ShowStake()
        {
            this.stake.Text = this.stock.SelectedItem is StockQuote quote && quote.Shares > 0
                ? string.Format(German, "{0:N2} % der Aktien", this.shares.Value * 100 / quote.Shares)
                : "";
        }

        // Writes the inputs to the slot (null: nothing selected, -1: a new position in a free slot).
        private void Save(int? slot)
        {
            if (!slot.HasValue || !(this.stock.SelectedItem is StockQuote quote) || !this.IsGameRunning())
                return;
            var position = new DepotPosition
            {
                Slot = slot.Value,
                Stock = quote.Index,
                Shares = (int)this.shares.Value,
                PurchasePrice = (float)this.purchasePrice.Value,
            };
            try
            {
                int written = this.game.SavePosition(this.Manager, position);
                if (written < 0)
                {
                    this.status.Text = "Das Depot ist voll";
                    return;
                }
                Logger.Debug($"Depotplatz {written} von Manager {this.Manager}: {position.Shares} × {quote.Name}");
                this.Reselect(written);
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Depot konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        private void Remove()
        {
            DepotPosition position = this.Selected();
            if (position == null || !this.IsGameRunning())
                return;
            try
            {
                this.game.RemovePosition(this.Manager, position.Slot);
                this.RefreshValues();
                this.status.Text = "Entfernt";
            }
            catch (Exception ex)
            {
                Logger.Error("Depotplatz konnte nicht geleert werden", ex);
                this.status.Text = "Fehler beim Entfernen";
            }
        }

        private void SetPrice()
        {
            if (!(this.stock.SelectedItem is StockQuote quote) || !this.IsGameRunning())
                return;
            int index = quote.Index;
            try
            {
                this.game.SetStockPrice(index, (float)this.price.Value);
                this.RefreshValues();
                this.stock.SelectedItem = this.Quote(index);
                this.status.Text = "Kurs gesetzt";
            }
            catch (Exception ex)
            {
                Logger.Error("Kurs konnte nicht gesetzt werden", ex);
                this.status.Text = "Fehler beim Setzen des Kurses";
            }
        }

        private void Reselect(int slot)
        {
            this.RefreshValues();
            ListViewItem item = this.depot.Items.Cast<ListViewItem>().FirstOrDefault(i => ((DepotPosition)i.Tag).Slot == slot);
            if (item != null)
                item.Selected = true;
        }

        private static NumericUpDown Price() => new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1000000,
            DecimalPlaces = 2,
            ThousandsSeparator = true,
        };

        private static void SetValue(NumericUpDown input, decimal value) =>
            input.Value = Math.Max(input.Minimum, Math.Min(input.Maximum, Math.Round(value, input.DecimalPlaces)));
    }
}
