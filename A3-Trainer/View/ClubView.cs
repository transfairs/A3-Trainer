using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A manager's club: name, city, money, image and standing with the association.</summary>
    internal sealed class ClubView : TrainerView
    {
        private readonly A3Game game;

        private readonly TextBox clubName = new TextBox { MaxLength = A3Addresses.ClubNameSize - 1 };
        private readonly TextBox clubShortName = new TextBox { MaxLength = A3Addresses.ClubShortNameSize - 1 };
        private readonly TextBox clubCity = new TextBox { MaxLength = A3Addresses.ClubNameSize - 1 };
        private readonly NumericUpDown seasonStartMoney = Money();
        private readonly NumericUpDown image = new NumericUpDown { Minimum = 0, Maximum = 255 };
        private readonly Label imageLevel = new Label();
        private readonly NumericUpDown trouble = new NumericUpDown { Minimum = 0, Maximum = Tables.AssociationTroubleMask };
        private readonly Label troubleLevel = new Label();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ClubInfo shown;

        /// <summary>Creates the club page, reading through <paramref name="game"/>.</summary>
        internal ClubView(ProcessMemory memory, A3Game game) : base(memory)
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
            var club = new GroupBox { Text = "Verein", Bounds = new Rectangle(8, 8, 614, 228) };
            int y = 22;
            void ClubRow(string caption, Control input)
            {
                club.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 170, 18) });
                input.SetBounds(190, y, 160, 21);
                club.Controls.Add(input);
                y += 32;
            }
            // A value with the level the game shows for it next to it.
            void Rated(string caption, NumericUpDown input, Label level, Func<int, string> name)
            {
                level.SetBounds(360, y + 3, 240, 18);
                club.Controls.Add(level);
                input.ValueChanged += (s, e) => level.Text = name((int)input.Value);
                ClubRow(caption, input);
            }
            ClubRow("Name", this.clubName);
            ClubRow("Kurzname", this.clubShortName);
            ClubRow("Stadt", this.clubCity);
            ClubRow("Vermögen Saisonbeginn (DM)", this.seasonStartMoney);
            Rated("Vereinsimage (10 bis 50)", this.image, this.imageLevel, Tables.ClubImage);
            Rated("Ärger mit dem Verband (0 bis 31)", this.trouble, this.troubleLevel, Tables.Association);
            this.Controls.Add(club);

            this.save.SetBounds(8, 244, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 244, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 248, 380, 20);
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
                this.imageLevel.Text = this.troubleLevel.Text = "";
                this.status.Text = "Kein Spiel verbunden";
                return;
            }

            this.clubName.Text = this.shown.Name;
            this.clubShortName.Text = this.shown.ShortName;
            this.clubCity.Text = this.shown.City;
            this.seasonStartMoney.Value = this.shown.SeasonStartMoney;
            this.image.Value = this.shown.Image;
            this.trouble.Value = this.shown.Trouble & Tables.AssociationTroubleMask;
            this.imageLevel.Text = Tables.ClubImage(this.shown.Image);
            this.troubleLevel.Text = Tables.Association(this.shown.Trouble & Tables.AssociationTroubleMask);
            this.status.Text = "";
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ClubInfo edited = this.shown.Clone();
            edited.SeasonStartMoney = (int)this.seasonStartMoney.Value;
            // An emptied name stays as it was.
            edited.Name = Filled(this.clubName.Text, this.shown.Name);
            edited.ShortName = Filled(this.clubShortName.Text, this.shown.ShortName);
            edited.City = Filled(this.clubCity.Text, this.shown.City);
            edited.Image = (int)this.image.Value;
            edited.Trouble = edited.Trouble & ~Tables.AssociationTroubleMask | (int)this.trouble.Value;
            try
            {
                this.game.SaveClubInfo(this.shown, edited);
                Logger.Debug("Verein gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Verein konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        private static string Filled(string text, string old) => text.Trim().Length > 0 ? text.Trim() : old;

        /// <summary>A money input: whole DM, thousands separators, steps of 100,000.</summary>
        internal static NumericUpDown Money() => new NumericUpDown
        {
            Minimum = int.MinValue,
            Maximum = int.MaxValue,
            ThousandsSeparator = true,
            Increment = 100000,
        };
    }
}
