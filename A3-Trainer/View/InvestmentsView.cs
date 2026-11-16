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
    /// <summary>A club's investments and the index of every investment partner.</summary>
    internal sealed class InvestmentsView : TrainerView
    {
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        private readonly A3Game game;

        private readonly ListView list = new ListView();
        private readonly ComboBox partner = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown amount = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, ThousandsSeparator = true, Increment = 100000, Value = 100000 };
        private readonly Button setValue = new Button { Text = "Wert setzen" };
        private readonly Button add = new Button { Text = "Neu anlegen" };
        private readonly NumericUpDown[] indices = Tables.InvestmentPartners.Select(_ => new NumericUpDown { Minimum = 1, Maximum = short.MaxValue }).ToArray();
        private readonly Button saveIndices = new Button { Text = "Indizes setzen" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private List<Investment> investments = new List<Investment>();
        private int[] shownIndices = new int[Tables.InvestmentPartners.Length];
        private bool found;

        /// <summary>Creates the investments page, reading through <paramref name="game"/>.</summary>
        internal InvestmentsView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(614, 389);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.partner.Items.AddRange(Tables.InvestmentPartners);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The club the page shows.</summary>
        internal ClubKey Club { get; set; }

        private void BuildLayout()
        {
            this.list.SetBounds(8, 8, 598, 130);
            this.list.View = System.Windows.Forms.View.Details;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.MultiSelect = false;
            this.list.Columns.Add("Anlagepartner", 230);
            this.list.Columns.Add("Eingezahlt (DM)", 120, HorizontalAlignment.Right);
            this.list.Columns.Add("Wert (DM)", 120, HorizontalAlignment.Right);
            this.list.Columns.Add("Gewinn (DM)", 104, HorizontalAlignment.Right);
            this.list.SelectedIndexChanged += (s, e) => this.ShowSelected();
            this.Controls.Add(this.list);

            var investment = new GroupBox { Text = "Anlage", Bounds = new Rectangle(8, 144, 598, 86) };
            investment.Controls.Add(new Label { Text = "Anlagepartner", Bounds = new Rectangle(12, 25, 110, 18) });
            this.partner.SetBounds(126, 22, 220, 21);
            investment.Controls.Add(new Label { Text = "Betrag (DM)", Bounds = new Rectangle(12, 55, 110, 18) });
            this.amount.SetBounds(126, 52, 140, 21);
            this.setValue.SetBounds(474, 20, 112, 25);
            this.setValue.Click += (s, e) => this.SetValue();
            this.add.SetBounds(474, 50, 112, 25);
            this.add.Click += (s, e) => this.Add();
            investment.Controls.AddRange(new Control[] { this.partner, this.amount, this.setValue, this.add });
            this.Controls.Add(investment);

            var indexBox = new GroupBox { Text = "Kursindex der Anlagepartner (für alle Vereine)", Bounds = new Rectangle(8, 236, 598, 98) };
            for (int i = 0; i < this.indices.Length; i++)
            {
                int x = 12 + i * 116;
                string name = Tables.InvestmentPartners[i];
                int bracket = name.IndexOf(" (", StringComparison.Ordinal);
                indexBox.Controls.Add(new Label { Text = bracket > 0 ? name.Substring(0, bracket) : name, Bounds = new Rectangle(x, 22, 112, 30), UseMnemonic = false });
                this.indices[i].SetBounds(x, 54, 100, 21);
                indexBox.Controls.Add(this.indices[i]);
            }
            this.Controls.Add(indexBox);

            this.saveIndices.SetBounds(8, 342, 110, 25);
            this.saveIndices.Click += (s, e) => this.SaveIndices();
            this.reload.SetBounds(124, 342, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 346, 366, 20);
            this.Controls.AddRange(new Control[] { this.saveIndices, this.reload, this.status });
        }

        /// <summary>Reads the club's investments and the partner indices from the game (or clears the view if there is no club).</summary>
        internal void RefreshValues()
        {
            int? selectedSlot = this.Selected()?.Slot;
            bool found = this.found = this.IsGameRunning(silent: true) && this.game.ClubAddress(this.Club).HasValue;
            this.investments = found ? this.game.ReadInvestments(this.Club) : new List<Investment>();
            this.shownIndices = found ? this.game.ReadInvestmentIndices() : new int[Tables.InvestmentPartners.Length];
            this.add.Enabled = this.saveIndices.Enabled = found;

            this.list.BeginUpdate();
            try
            {
                this.list.Items.Clear();
                foreach (Investment investment in this.investments)
                {
                    long value = investment.ValueAt(this.Index(investment.Partner));
                    this.list.Items.Add(new ListViewItem(new[]
                    {
                        investment.Partner < Tables.InvestmentPartners.Length ? Tables.InvestmentPartners[investment.Partner] : $"Partner {investment.Partner}",
                        investment.Paid.ToString("N0", German),
                        value.ToString("N0", German),
                        (value - investment.Paid).ToString("N0", German),
                    }) { Tag = investment, Selected = investment.Slot == selectedSlot });
                }
            }
            finally
            {
                this.list.EndUpdate();
            }
            for (int i = 0; i < this.indices.Length; i++)
                this.indices[i].Value = Math.Max(this.indices[i].Minimum, Math.Min(this.indices[i].Maximum, this.shownIndices[i]));
            this.ShowSelected();
            this.status.Text = found ? "" : "Kein Verein gefunden";
        }

        private int Index(int partner) => partner < this.shownIndices.Length ? this.shownIndices[partner] : 0;

        private Investment Selected() => this.list.SelectedItems.Count > 0 ? this.list.SelectedItems[0].Tag as Investment : null;

        // A selected investment shows its partner and value; without one the inputs are for a new investment.
        private void ShowSelected()
        {
            Investment investment = this.Selected();
            this.setValue.Enabled = investment != null;
            this.partner.Enabled = investment == null;
            if (investment == null)
            {
                if (this.partner.SelectedIndex < 0)
                    this.partner.SelectedIndex = 0;
                return;
            }
            this.partner.SelectedIndex = investment.Partner < this.partner.Items.Count ? investment.Partner : -1;
            this.amount.Value = Math.Max(this.amount.Minimum, Math.Min(this.amount.Maximum, investment.ValueAt(this.Index(investment.Partner))));
        }

        private void SetValue()
        {
            Investment investment = this.Selected();
            if (investment == null || !this.IsGameRunning())
                return;
            this.Run(() => this.game.SetInvestmentValue(this.Club, investment, (long)this.amount.Value), "Wert gesetzt");
        }

        private void Add()
        {
            if (!this.found || this.partner.SelectedIndex < 0 || this.amount.Value <= 0 || !this.IsGameRunning())
                return;
            int slot = -1;
            this.Run(() => slot = this.game.AddInvestment(this.Club, this.partner.SelectedIndex, (long)this.amount.Value), "Angelegt");
            if (slot < 0 && this.status.Text == "Angelegt")
                this.status.Text = "Alle sechs Anlageplätze sind belegt";
        }

        private void SaveIndices()
        {
            if (!this.found || !this.IsGameRunning())
                return;
            int[] edited = this.indices.Select(i => (int)i.Value).ToArray();
            this.Run(() => this.game.SaveInvestmentIndices(this.shownIndices, edited), "Indizes gesetzt");
        }

        private void Run(Action action, string done)
        {
            try
            {
                action();
                Logger.Debug($"Anlagen: {done} ({this.Club})");
                this.RefreshValues();
                this.status.Text = done;
            }
            catch (Exception ex)
            {
                Logger.Error("Anlagen konnten nicht geändert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }
    }
}
