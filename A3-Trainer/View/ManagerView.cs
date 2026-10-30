using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>Human manager 1: name, salary, private money.</summary>
    internal sealed class ManagerView : TrainerView
    {
        private readonly A3Game game;

        private readonly TextBox managerName = new TextBox { ReadOnly = true };
        private readonly TextBox managerSalary = new TextBox { ReadOnly = true };
        private readonly NumericUpDown privateMoney = ClubView.Money();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ClubInfo shown;

        /// <summary>Creates the manager page, reading through <paramref name="game"/>.</summary>
        internal ManagerView(ProcessMemory memory, A3Game game) : base(memory)
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
            var manager = new GroupBox { Text = "Manager (Spieler 1)", Bounds = new Rectangle(8, 8, 614, 110) };
            int y = 22;
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

            this.save.SetBounds(8, 126, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 126, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 130, 380, 20);
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

            this.managerName.Text = (this.shown.ManagerFirstName + " " + this.shown.ManagerLastName).Trim();
            this.managerSalary.Text = this.shown.ManagerSalary.ToString("N0");
            this.privateMoney.Value = this.shown.ManagerPrivateMoney;
            this.status.Text = "";
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ClubInfo edited = this.shown.Clone();
            edited.ManagerPrivateMoney = (int)this.privateMoney.Value;
            try
            {
                this.game.SaveClubInfo(this.shown, edited);
                Logger.Debug("Manager gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Manager konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }
    }
}
