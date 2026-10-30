using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>Own club and human manager 1: names, money.</summary>
    internal sealed class ClubView : TrainerView
    {
        private readonly A3Game game;

        private readonly TextBox clubName = ReadOnly();
        private readonly TextBox clubCity = ReadOnly();
        private readonly NumericUpDown seasonStartMoney = Money();
        private readonly TextBox managerName = ReadOnly();
        private readonly TextBox managerSalary = ReadOnly();
        private readonly NumericUpDown privateMoney = Money();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ClubInfo shown;

        internal ClubView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        private void BuildLayout()
        {
            var club = new GroupBox { Text = "Verein", Bounds = new Rectangle(8, 8, 614, 132) };
            int y = 22;
            void ClubRow(string caption, Control input)
            {
                club.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 170, 18) });
                input.SetBounds(190, y, 160, 21);
                club.Controls.Add(input);
                y += 32;
            }
            ClubRow("Name", this.clubName);
            ClubRow("Stadt", this.clubCity);
            ClubRow("Vermögen Saisonbeginn (DM)", this.seasonStartMoney);
            this.Controls.Add(club);

            var manager = new GroupBox { Text = "Manager (Spieler 1)", Bounds = new Rectangle(8, 148, 614, 110) };
            y = 22;
            void ManagerRow(string caption, Control input)
            {
                manager.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 170, 18) });
                input.SetBounds(190, y, 160, 21);
                manager.Controls.Add(input);
                y += 28;
            }
            ManagerRow("Name", this.managerName);
            ManagerRow("Festgehalt (DM pro Jahr)", this.managerSalary);
            ManagerRow("Privates Vermögen (DM)", this.privateMoney);
            this.Controls.Add(manager);

            this.save.SetBounds(8, 266, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 266, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 270, 380, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the values from the game (or clears the view if none is attached).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadClubInfo() : null;
            this.save.Enabled = this.shown != null;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.status.Text = "Kein Spiel verbunden";
                return;
            }

            this.clubName.Text = this.shown.Name;
            this.clubCity.Text = this.shown.City;
            SetMoney(this.seasonStartMoney, this.shown.SeasonStartMoney);
            this.managerName.Text = (this.shown.ManagerFirstName + " " + this.shown.ManagerLastName).Trim();
            this.managerSalary.Text = this.shown.ManagerSalary.ToString("N0");
            SetMoney(this.privateMoney, this.shown.ManagerPrivateMoney);
            this.status.Text = "";
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            var edited = new ClubInfo
            {
                SeasonStartMoney = (int)this.seasonStartMoney.Value,
                ManagerPrivateMoney = (int)this.privateMoney.Value,
            };
            try
            {
                this.game.SaveClubInfo(this.shown, edited);
                Logger.Debug("Verein/Manager gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Verein/Manager konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        private static TextBox ReadOnly() => new TextBox { ReadOnly = true };

        private static NumericUpDown Money() => new NumericUpDown
        {
            Minimum = int.MinValue,
            Maximum = int.MaxValue,
            ThousandsSeparator = true,
            Increment = 100000,
        };

        private static void SetMoney(NumericUpDown number, int value) => number.Value = value;
    }
}
