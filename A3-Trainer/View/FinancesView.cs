using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A club's overdraft and loans, and the interest rates every club pays or gets.</summary>
    internal sealed class FinancesView : TrainerView
    {
        private readonly A3Game game;

        private readonly NumericUpDown overdraft = new NumericUpDown
        {
            Minimum = 0,
            Maximum = ushort.MaxValue * (decimal)A3Addresses.OverdraftUnit / 1000000,
            DecimalPlaces = 1,
            Increment = 0.1m,
        };
        private readonly Label loans = new Label();
        private readonly Button forgive = new Button { Text = "Kredite erlassen" };
        private readonly Label legalForm = new Label();
        private readonly NumericUpDown shares = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = 1000000, ThousandsSeparator = true, Increment = 100000 };
        private readonly NumericUpDown price = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = 100, DecimalPlaces = 2 };
        private readonly Button flotation = new Button { Text = "Zur AG machen" };
        private readonly NumericUpDown interest = Rate();
        private readonly NumericUpDown overdraftRate = Rate();
        private readonly NumericUpDown savings = Rate();
        private readonly NumericUpDown loanRate = Rate();
        private readonly NumericUpDown depositRate = Rate();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ClubFinances shown;
        private InterestRates rates;

        /// <summary>Creates the finances page, reading through <paramref name="game"/>.</summary>
        internal FinancesView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(614, 389);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The club the page shows.</summary>
        internal ClubKey Club { get; set; }

        private void BuildLayout()
        {
            int y = 22;
            void Row(GroupBox group, string caption, Control input, string note)
            {
                group.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 190, 18) });
                input.SetBounds(210, y, 100, 21);
                group.Controls.Add(input);
                if (note != null)
                    group.Controls.Add(new Label { Text = note, Bounds = new Rectangle(318, y + 3, 270, 18), UseMnemonic = false });
                y += 30;
            }

            var account = new GroupBox { Text = "Konto", Bounds = new Rectangle(8, 6, 598, 142) };
            Row(account, "Dispokredit (Mio DM)", this.overdraft, "auf der Bilanz in Klammern");
            account.Controls.Add(new Label { Text = "Kredite", Bounds = new Rectangle(12, y + 3, 190, 18) });
            this.loans.SetBounds(210, y + 3, 230, 18);
            this.forgive.SetBounds(446, y - 2, 140, 25);
            this.forgive.Click += (s, e) => this.Forgive();
            y += 30;
            account.Controls.Add(new Label { Text = "Rechtsform", Bounds = new Rectangle(12, y + 3, 190, 18) });
            this.legalForm.SetBounds(210, y + 3, 230, 18);
            y += 30;
            account.Controls.Add(new Label { Text = "Aktien / Ausgabekurs (DM)", Bounds = new Rectangle(12, y + 3, 190, 18) });
            this.shares.SetBounds(210, y, 100, 21);
            this.price.SetBounds(318, y, 80, 21);
            this.flotation.SetBounds(446, y - 2, 140, 25);
            this.flotation.Click += (s, e) => this.Float();
            account.Controls.AddRange(new Control[] { this.loans, this.forgive, this.legalForm, this.shares, this.price, this.flotation });
            this.Controls.Add(account);

            var interestRates = new GroupBox { Text = "Zinsen (für alle Vereine, in %)", Bounds = new Rectangle(8, 154, 598, 172) };
            y = 22;
            Row(interestRates, "Zinssatz", this.interest, "über den Dispokredit hinaus");
            Row(interestRates, "Dispozins", this.overdraftRate, "innerhalb des Dispokredits (Bilanz: Dispokredit)");
            Row(interestRates, "Guthabenzins", this.savings, "auf ein Guthaben");
            Row(interestRates, "Kreditzins", this.loanRate, "für neue Kredite, längere Laufzeit weniger");
            Row(interestRates, "Tagesgeldzins", this.depositRate, "für Tagesgeld bei Sorgenfrei & Sohn (Anlagen)");
            this.Controls.Add(interestRates);

            this.save.SetBounds(8, 332, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 332, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 336, 366, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the club's finances and the rates from the game (or clears the view if there is no club).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadFinances(this.Club) : null;
            this.save.Enabled = this.forgive.Enabled = this.flotation.Enabled = this.shown != null;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.loans.Text = this.legalForm.Text = "";
                this.status.Text = "Kein Verein gefunden";
                return;
            }

            this.rates = this.game.ReadRates();
            SetValue(this.overdraft, (decimal)this.shown.Overdraft / 1000000);
            this.loans.Text = this.shown.LoanCount == 0
                ? "keine"
                : string.Format(CultureInfo.GetCultureInfo("de-DE"), "{0} über {1:N0} DM", this.shown.LoanCount, this.shown.LoanTotal);
            this.forgive.Enabled = this.shown.LoanCount > 0;
            this.legalForm.Text = this.shown.IsPublic ? "Aktiengesellschaft" : "Verein";
            this.flotation.Enabled = this.shares.Enabled = this.price.Enabled = !this.shown.IsPublic;
            SetValue(this.interest, this.rates.Interest / 100m);
            SetValue(this.overdraftRate, this.rates.Overdraft / 100m);
            SetValue(this.savings, this.rates.Savings / 100m);
            SetValue(this.loanRate, this.rates.Loan / 100m);
            SetValue(this.depositRate, this.rates.Deposit / 100m);
            this.status.Text = "";
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ClubFinances edited = this.shown.Clone();
            edited.Overdraft = (int)Math.Round(this.overdraft.Value * 1000000 / A3Addresses.OverdraftUnit) * A3Addresses.OverdraftUnit;
            InterestRates newRates = this.rates.Clone();
            newRates.Interest = Hundredths(this.interest);
            newRates.Overdraft = Hundredths(this.overdraftRate);
            newRates.Savings = Hundredths(this.savings);
            newRates.Loan = Hundredths(this.loanRate);
            newRates.Deposit = Hundredths(this.depositRate);
            try
            {
                this.game.SaveFinances(this.shown, edited);
                this.game.SaveRates(this.rates, newRates);
                Logger.Debug("Finanzen gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Finanzen konnten nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        private void Forgive()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;
            try
            {
                long forgiven = this.game.ForgiveLoans(this.Club);
                Logger.Debug($"Kredite über {forgiven} DM erlassen ({this.Club})");
                this.RefreshValues();
                this.status.Text = string.Format(CultureInfo.GetCultureInfo("de-DE"), "{0:N0} DM Kredite erlassen", forgiven);
            }
            catch (Exception ex)
            {
                Logger.Error("Kredite konnten nicht erlassen werden", ex);
                this.status.Text = "Fehler beim Erlassen";
            }
        }

        private void Float()
        {
            if (this.shown == null || this.shown.IsPublic || !this.IsGameRunning())
                return;
            try
            {
                bool done = this.game.MakePublic(this.Club, (int)this.shares.Value, (float)this.price.Value);
                Logger.Debug($"Börsengang {(done ? "erfolgt" : "nicht möglich")} ({this.Club})");
                this.RefreshValues();
                this.status.Text = done ? "Der Verein ist jetzt eine AG" : "Kein Platz mehr im Aktienmarkt";
            }
            catch (Exception ex)
            {
                Logger.Error("Börsengang fehlgeschlagen", ex);
                this.status.Text = "Fehler beim Börsengang";
            }
        }

        private static int Hundredths(NumericUpDown input) => (int)Math.Round(input.Value * 100);

        private static NumericUpDown Rate() => new NumericUpDown
        {
            Minimum = 0,
            Maximum = short.MaxValue / 100m,
            DecimalPlaces = 2,
            Increment = 0.25m,
        };

        private static void SetValue(NumericUpDown input, decimal value) =>
            input.Value = Math.Max(input.Minimum, Math.Min(input.Maximum, value));
    }
}
