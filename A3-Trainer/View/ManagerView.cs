using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A human manager: name, private money, board trust, popularity and contract.</summary>
    internal sealed class ManagerView : TrainerView
    {
        private readonly A3Game game;

        private readonly TextBox firstName = new TextBox { MaxLength = A3Addresses.ManagerFirstNameSize - 1 };
        private readonly TextBox lastName = new TextBox { MaxLength = A3Addresses.ManagerLastNameSize - 1 };
        private readonly NumericUpDown managerSalary = ClubView.Money();
        private readonly NumericUpDown privateMoney = ClubView.Money();
        private readonly NumericUpDown pointBonus = ClubView.Money();
        private readonly NumericUpDown cupBonus = ClubView.Money();
        private readonly NumericUpDown titleBonus = ClubView.Money();
        private readonly NumericUpDown goalBonus = ClubView.Money();
        private readonly CheckBox clubOption = new CheckBox { Text = "Option Verein auf Verlängerung" };
        private readonly NumericUpDown contractYears = new NumericUpDown { Minimum = 0, Maximum = 20 };
        private readonly CheckBox exitClause = new CheckBox { Text = "Ausstieg bei Angebot möglich" };
        private readonly NumericUpDown media = Percent();
        private readonly NumericUpDown publicView = Percent();
        private readonly NumericUpDown fans = Percent();
        private readonly NumericUpDown boardTrust = Percent();
        private readonly Label mediaLevel = new Label();
        private readonly Label publicLevel = new Label();
        private readonly Label fansLevel = new Label();
        private readonly Label boardLevel = new Label();
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

        /// <summary>The human manager (1-4) whose data the page shows.</summary>
        internal int Manager { get; set; } = 1;

        private void BuildLayout()
        {
            var manager = new GroupBox { Text = "Manager", Bounds = new Rectangle(8, 8, 614, 222) };
            int y = 22;
            void ManagerRow(string caption, Control input, Label note = null)
            {
                manager.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 170, 18) });
                input.SetBounds(190, y, 160, 21);
                manager.Controls.Add(input);
                if (note != null)
                {
                    note.SetBounds(360, y + 3, 250, 18);
                    manager.Controls.Add(note);
                }
                y += 28;
            }
            ManagerRow("Vorname", this.firstName);
            ManagerRow("Nachname", this.lastName);
            ManagerRow("Privates Vermögen (DM)", this.privateMoney);
            ManagerRow("Vorstandsvertrauen (%)", this.boardTrust, this.boardLevel);
            ManagerRow("Beliebtheit bei den Medien", this.media, this.mediaLevel);
            ManagerRow("in der Öffentlichkeit", this.publicView, this.publicLevel);
            ManagerRow("bei den Fans (Grundwert)", this.fans, this.fansLevel);
            this.media.ValueChanged += (s, e) => this.mediaLevel.Text = Tables.Popularity((int)this.media.Value);
            this.publicView.ValueChanged += (s, e) => this.publicLevel.Text = Tables.Popularity((int)this.publicView.Value);
            // The fans see the base weighted by the manager's image; the level is the one the game shows.
            this.fans.ValueChanged += (s, e) => this.fansLevel.Text = Tables.Popularity(this.shown?.ShownFans((int)this.fans.Value) ?? 0);
            this.boardTrust.ValueChanged += (s, e) => this.boardLevel.Text = Tables.BoardTrust((int)this.boardTrust.Value, this.shown?.Name ?? "");
            this.Controls.Add(manager);

            // Contract in two columns, as on the career score's contract page.
            var contract = new GroupBox { Text = "Vertrag", Bounds = new Rectangle(8, 236, 614, 140) };
            int row = 0;
            void ContractRow(int column, string caption, Control input)
            {
                int x = column == 0 ? 12 : 316;
                int top = 22 + (row % 4) * 28;
                contract.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(x, top + 3, 130, 18) });
                input.SetBounds(x + 135, top, 150, 21);
                contract.Controls.Add(input);
                row++;
            }
            ContractRow(0, "Festgehalt (DM/Jahr)", this.managerSalary);
            ContractRow(0, "Punktprämie (DM)", this.pointBonus);
            ContractRow(0, "je Pokalrunde (DM)", this.cupBonus);
            ContractRow(0, "je Titel / Aufstieg (DM)", this.titleBonus);
            ContractRow(1, "Bonus bei Erreichen (DM)", this.goalBonus);
            ContractRow(1, "Laufzeit (Jahre)", this.contractYears);
            this.exitClause.SetBounds(316, 22 + 2 * 28, 280, 21);
            this.clubOption.SetBounds(316, 22 + 3 * 28, 280, 21);
            contract.Controls.AddRange(new Control[] { this.exitClause, this.clubOption });
            this.Controls.Add(contract);

            this.save.SetBounds(8, 384, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 384, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 388, 380, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the values from the game (or clears the view if none is attached).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadClubInfo(this.Manager) : null;
            this.save.Enabled = this.shown != null;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.mediaLevel.Text = this.publicLevel.Text = this.fansLevel.Text = this.boardLevel.Text = "";
                this.status.Text = "Kein Spiel verbunden";
                return;
            }

            this.firstName.Text = this.shown.ManagerFirstName;
            this.lastName.Text = this.shown.ManagerLastName;
            this.managerSalary.Value = this.shown.ManagerSalary;
            this.privateMoney.Value = this.shown.ManagerPrivateMoney;
            this.pointBonus.Value = this.shown.PointBonus;
            this.cupBonus.Value = this.shown.CupBonus;
            this.titleBonus.Value = this.shown.TitleBonus;
            this.goalBonus.Value = this.shown.GoalBonus;
            this.contractYears.Value = Math.Min(this.shown.ContractYears, (int)this.contractYears.Maximum);
            this.exitClause.Checked = this.shown.ExitClause;
            this.clubOption.Checked = this.shown.ClubOption;
            this.boardTrust.Value = this.shown.BoardTrust;
            this.media.Value = Math.Min(this.shown.MediaPopularity, 100);
            this.publicView.Value = Math.Min(this.shown.PublicPopularity, 100);
            this.fans.Value = Math.Min(this.shown.FanPopularity, 100);
            this.mediaLevel.Text = Tables.Popularity(this.shown.MediaPopularity);
            this.publicLevel.Text = Tables.Popularity(this.shown.PublicPopularity);
            this.fansLevel.Text = Tables.Popularity(this.shown.ShownFanPopularity);
            this.boardLevel.Text = Tables.BoardTrust(this.shown.BoardTrust, this.shown.Name);
            this.status.Text = "";
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ClubInfo edited = this.shown.Clone();
            // An emptied name stays as it was.
            if (this.firstName.Text.Trim().Length > 0)
                edited.ManagerFirstName = this.firstName.Text.Trim();
            if (this.lastName.Text.Trim().Length > 0)
                edited.ManagerLastName = this.lastName.Text.Trim();
            edited.ManagerSalary = (int)this.managerSalary.Value;
            edited.ManagerPrivateMoney = (int)this.privateMoney.Value;
            edited.PointBonus = (int)this.pointBonus.Value;
            edited.CupBonus = (int)this.cupBonus.Value;
            edited.TitleBonus = (int)this.titleBonus.Value;
            edited.GoalBonus = (int)this.goalBonus.Value;
            // A term above the field's limit stays unless it is changed.
            if ((int)this.contractYears.Value != Math.Min(this.shown.ContractYears, (int)this.contractYears.Maximum))
                edited.ContractYears = (int)this.contractYears.Value;
            edited.ExitClause = this.exitClause.Checked;
            edited.ClubOption = this.clubOption.Checked;
            // The game adds its own bonuses to the stored base; the base is set so the total matches.
            if ((int)this.boardTrust.Value != this.shown.BoardTrust)
                edited.BoardTrustBase = (int)this.boardTrust.Value - this.shown.BoardTrustBonus;
            // A stored value above 100 (the field's limit) stays unless it is changed.
            edited.MediaPopularity = Keep(this.shown.MediaPopularity, this.media);
            edited.PublicPopularity = Keep(this.shown.PublicPopularity, this.publicView);
            edited.FanPopularity = Keep(this.shown.FanPopularity, this.fans);
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

        private static NumericUpDown Percent() => new NumericUpDown { Minimum = 0, Maximum = 100 };

        private static int Keep(int stored, NumericUpDown input) =>
            (int)input.Value == Math.Min(stored, 100) ? stored : (int)input.Value;
    }
}
