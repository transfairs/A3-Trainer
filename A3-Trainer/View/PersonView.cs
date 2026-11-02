using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A human manager as a person: birthday, manager level and competencies.</summary>
    internal sealed class PersonView : TrainerView
    {
        private const int MaxCompetency = 15;

        private readonly A3Game game;

        private readonly NumericUpDown birthDay = new NumericUpDown { Minimum = 0, Maximum = 31 };
        private readonly NumericUpDown birthMonth = new NumericUpDown { Minimum = 0, Maximum = 12 };
        private readonly NumericUpDown birthYear = new NumericUpDown { Minimum = 0, Maximum = 3000 };
        private readonly Label age = new Label();
        private readonly NumericUpDown level = new NumericUpDown { Minimum = 0, Maximum = A3Addresses.ManagerLevels - 1 };
        private readonly Label levelInForce = new Label();
        private readonly GroupBox competencyGroup = new GroupBox();
        private readonly NumericUpDown[] competencies =
            Tables.Competencies.Select(_ => new NumericUpDown { Minimum = 0, Maximum = MaxCompetency }).ToArray();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ManagerPerson shown;
        // The competency table with the changes made so far; the inputs show the row of shownRow.
        private int[][] table;
        private int shownRow = -1;
        private GameDate? today;

        /// <summary>Creates the person page, reading through <paramref name="game"/>.</summary>
        internal PersonView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(614, 389);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The human manager (1-4) whose data the page shows.</summary>
        internal int Manager { get; set; } = 1;

        private void BuildLayout()
        {
            var person = new GroupBox { Text = "Person", Bounds = new Rectangle(8, 6, 598, 84) };
            person.Controls.Add(new Label { Text = "Geburtstag", Bounds = new Rectangle(12, 25, 170, 18) });
            this.birthDay.SetBounds(190, 22, 46, 21);
            this.birthMonth.SetBounds(240, 22, 46, 21);
            this.birthYear.SetBounds(290, 22, 62, 21);
            person.Controls.AddRange(new Control[] { this.birthDay, this.birthMonth, this.birthYear });
            person.Controls.Add(new Label { Text = "Alter", Bounds = new Rectangle(12, 53, 170, 18) });
            this.age.SetBounds(190, 53, 160, 18);
            person.Controls.Add(this.age);
            foreach (NumericUpDown part in new[] { this.birthDay, this.birthMonth, this.birthYear })
                part.ValueChanged += (s, e) => this.age.Text = this.AgeText();
            this.Controls.Add(person);

            var levels = new GroupBox { Text = "Managerlevel", Bounds = new Rectangle(8, 96, 598, 56) };
            levels.Controls.Add(new Label { Text = "Level", Bounds = new Rectangle(12, 25, 170, 18) });
            this.level.SetBounds(190, 22, 60, 21);
            this.levelInForce.SetBounds(260, 25, 330, 18);
            levels.Controls.AddRange(new Control[] { this.level, this.levelInForce });
            this.level.ValueChanged += (s, e) => this.ShowRow();
            this.Controls.Add(levels);

            this.competencyGroup.Bounds = new Rectangle(8, 158, 598, 192);
            for (int c = 0; c < this.competencies.Length; c++)
            {
                int y = 20 + c * 24;
                this.competencyGroup.Controls.Add(new Label { Text = Tables.Competencies[c], Bounds = new Rectangle(12, y + 3, 170, 18) });
                this.competencies[c].SetBounds(190, y, 60, 21);
                this.competencyGroup.Controls.Add(this.competencies[c]);
            }
            this.Controls.Add(this.competencyGroup);

            this.save.SetBounds(8, 356, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 356, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 360, 366, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the values from the game (or clears the view if none is attached).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadPerson(this.Manager) : null;
            this.save.Enabled = this.shown != null;
            this.shownRow = -1;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.age.Text = this.levelInForce.Text = this.competencyGroup.Text = "";
                this.status.Text = "Kein Spiel verbunden";
                return;
            }

            this.today = this.game.Today();
            this.table = this.shown.Clone().Competencies;
            GameDate birthday = this.shown.Birthday;
            SetNumber(this.birthDay, birthday.IsValid ? birthday.Day : 0);
            SetNumber(this.birthMonth, birthday.IsValid ? birthday.Month : 0);
            SetNumber(this.birthYear, birthday.IsValid ? birthday.Year : 0);
            this.age.Text = this.AgeText();
            SetNumber(this.level, this.shown.Level);
            this.ShowRow();
            this.status.Text = "";
        }

        // Shows the competencies of the level in force for the entered level, keeping what was entered for the last one.
        private void ShowRow()
        {
            if (this.shown == null)
                return;
            this.TakeRow();
            this.shownRow = this.shown.LevelInForce((int)this.level.Value);
            this.levelInForce.Text = this.shownRow == (int)this.level.Value ? "" : $"im Spiel: Level {this.shownRow}";
            this.competencyGroup.Text = $"Fähigkeiten auf Level {this.shownRow}";
            for (int c = 0; c < this.competencies.Length; c++)
                SetNumber(this.competencies[c], this.table[this.shownRow][c]);
        }

        // A stored value outside the inputs' range stays unless it is changed.
        private void TakeRow()
        {
            if (this.shownRow < 0)
                return;
            int[] row = this.table[this.shownRow];
            for (int c = 0; c < this.competencies.Length; c++)
                if ((int)this.competencies[c].Value != Math.Min(Math.Max(row[c], 0), MaxCompetency))
                    row[c] = (int)this.competencies[c].Value;
        }

        // Setting the value it already has doesn't redraw the text, which ClearAllFields may have emptied.
        private static void SetNumber(NumericUpDown number, int value)
        {
            number.Value = Math.Min(Math.Max(value, (int)number.Minimum), (int)number.Maximum);
            number.Text = number.Value.ToString();
        }

        private GameDate EnteredBirthday() =>
            new GameDate((int)this.birthYear.Value, (int)this.birthMonth.Value, (int)this.birthDay.Value);

        private string AgeText() =>
            this.today.HasValue && this.EnteredBirthday().Exists ? this.EnteredBirthday().AgeOn(this.today.Value).ToString() : "";

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ManagerPerson edited = this.shown.Clone();
            edited.Birthday = this.EnteredBirthday();
            if (!edited.Birthday.Equals(this.shown.Birthday) && !edited.Birthday.Exists)
            {
                this.status.Text = "Ungültiges Geburtsdatum";
                return;
            }
            if ((int)this.level.Value != Math.Min(this.shown.Level, (int)this.level.Maximum))
                edited.Level = (int)this.level.Value;
            this.TakeRow();
            edited.Competencies = this.table.Select(row => (int[])row.Clone()).ToArray();
            try
            {
                this.game.SavePerson(this.shown, edited);
                Logger.Debug("Manager (Person) gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Manager (Person) konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }
    }
}
